using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using PortfolioManager.Models;

namespace PortfolioManager.Services;

public class PortfolioCalculationService
{
    private readonly ILogger<PortfolioCalculationService> _logger;
    private readonly MongoDbService _mongoDbService;
    private readonly IDistributedCache _cache;
    private const int DECIMAL_PLACES = 4;
    private const string BASE_CURRENCY = "TWD";
    private const string USD_CURRENCY = "USD";

    public PortfolioCalculationService(
        ILogger<PortfolioCalculationService> logger,
        MongoDbService mongoDbService,
        IDistributedCache cache)
    {
        _logger = logger;
        _mongoDbService = mongoDbService;
        _cache = cache;
    }

    public async Task<PortfolioMetrics> CalculateMetrics(Portfolio portfolio, IDictionary<string, decimal> stockPrices = null)
    {
        try
        {
            var metrics = new PortfolioMetrics
            {
                PortfolioId = portfolio.Id,
                TotalValue = 0,
                StockMetrics = new List<StockMetric>(),
                BaseCurrency = BASE_CURRENCY
            };

            // 1. 獲取所有相關股票的最新價格和資訊
            var stockIds = portfolio.Stocks.Select(s => s.StockId).ToList();
            IDictionary<string, Stock> stocks;

            if (stockPrices == null)
            {
                var stocksList = await _mongoDbService.Stocks
                    .Find(s => stockIds.Contains(s.Id))
                    .ToListAsync();
                stocks = stocksList.ToDictionary(s => s.Id);
                stockPrices = stocks.ToDictionary(s => s.Key, s => s.Value.Price);
            }
            else
            {
                var stocksList = await _mongoDbService.Stocks
                    .Find(s => stockIds.Contains(s.Id))
                    .ToListAsync();
                stocks = stocksList.ToDictionary(s => s.Id);
            }

            // 2. 計算每個股票的價值，考慮匯率轉換
            foreach (var position in portfolio.Stocks)
            {
                if (stockPrices.TryGetValue(position.StockId, out decimal price) && 
                    stocks.TryGetValue(position.StockId, out Stock stock))
                {
                    // 根據貨幣進行匯率轉換
                    decimal convertedPrice = price;
                    if (stock.Currency == USD_CURRENCY)
                    {
                        convertedPrice = decimal.Round(
                            (decimal)(price * portfolio.ExchangeRate),
                            DECIMAL_PLACES,
                            MidpointRounding.AwayFromZero
                        );
                    }

                    var currentValue = decimal.Round(
                        position.Quantity * convertedPrice,
                        DECIMAL_PLACES,
                        MidpointRounding.AwayFromZero
                    );
                    
                    metrics.TotalValue += currentValue;

                    var stockMetric = new StockMetric
                    {
                        StockId = position.StockId,
                        StockName = stock.Name,
                        Quantity = position.Quantity,
                        OriginalPrice = price,
                        CurrentPrice = convertedPrice,
                        CurrentValue = currentValue,
                        Currency = stock.Currency,
                        OriginalValue = decimal.Round(
                            position.Quantity * price,
                            DECIMAL_PLACES,
                            MidpointRounding.AwayFromZero
                        )
                    };

                    // 如果是USD，添加匯率相關資訊
                    if (stock.Currency == USD_CURRENCY)
                    {
                        stockMetric.ExchangeRate = portfolio.ExchangeRate;
                        stockMetric.ConvertedToCurrency = BASE_CURRENCY;
                    }

                    metrics.StockMetrics.Add(stockMetric);
                }
                else
                {
                    _logger.LogWarning($"Stock {position.StockId} not found in prices or stocks dictionary");
                }
            }

            // 3. 計算百分比
            if (metrics.TotalValue > 0)
            {
                foreach (var metric in metrics.StockMetrics)
                {
                    metric.PercentageOfPortfolio = decimal.Round(
                        (metric.CurrentValue / metrics.TotalValue) * 100,
                        2,
                        MidpointRounding.AwayFromZero
                    );
                }

                // 確保百分比總和為 100%
                var totalPercentage = metrics.StockMetrics.Sum(m => m.PercentageOfPortfolio);
                if (totalPercentage != 100m && metrics.StockMetrics.Any())
                {
                    var largestPosition = metrics.StockMetrics
                        .OrderByDescending(m => m.CurrentValue)
                        .First();
                    largestPosition.PercentageOfPortfolio += (100m - totalPercentage);
                }
            }

            // 4. 計算貨幣分布
            metrics.CurrencyDistribution = metrics.StockMetrics
                .GroupBy(m => m.Currency)
                .ToDictionary(
                    g => g.Key,
                    g => new CurrencyMetric
                    {
                        TotalValue = decimal.Round(g.Sum(m => m.CurrentValue), 2),
                        Percentage = decimal.Round(g.Sum(m => m.CurrentValue) / metrics.TotalValue * 100, 2)
                    }
                );

            // 5. 設置其他指標
            metrics.LastUpdated = DateTime.UtcNow;
            metrics.NumberOfStocks = metrics.StockMetrics.Count;
            metrics.TotalValue = decimal.Round(metrics.TotalValue, 2, MidpointRounding.AwayFromZero);
            metrics.ExchangeRate = (decimal)portfolio.ExchangeRate;

            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error calculating metrics for portfolio {portfolio.Id}");
            throw;
        }
    }

    public async Task UpdatePortfolioValues(Portfolio portfolio, IDictionary<string, decimal> stockPrices)
    {
        try
        {
            // 1. 計算新的指標
            var metrics = await CalculateMetrics(portfolio, stockPrices);

            // 2. 更新 Portfolio 中的持倉百分比和價值
            foreach (var position in portfolio.Stocks)
            {
                var metric = metrics.StockMetrics.FirstOrDefault(m => m.StockId == position.StockId);
                if (metric != null)
                {
                    position.PercentageOfTotal = metric.PercentageOfPortfolio;
                }
            }

            // 3. 構建更新操作
            var update = Builders<Portfolio>.Update
                .Set(p => p.TotalValue, metrics.TotalValue)
                .Set(p => p.Stocks, portfolio.Stocks)
                .Set(p => p.LastUpdated, DateTime.UtcNow);

            // 4. 執行更新
            var result = await _mongoDbService.Portfolios.UpdateOneAsync(
                p => p.Id == portfolio.Id,
                update
            );

            if (result.ModifiedCount == 0)
            {
                _logger.LogWarning($"No changes were made to portfolio {portfolio.Id}");
            }
            else
            {
                _logger.LogInformation(
                    $"Successfully updated portfolio {portfolio.Id} with new total value {metrics.TotalValue} {BASE_CURRENCY}"
                );
            }

            // 5. 清除快取
            await _cache.RemoveAsync($"portfolio:{portfolio.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating portfolio {portfolio.Id} values");
            throw;
        }
    }
}

public class PortfolioMetrics
{
    public string PortfolioId { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime LastUpdated { get; set; }
    public int NumberOfStocks { get; set; }
    public List<StockMetric> StockMetrics { get; set; }
    public string BaseCurrency { get; set; }
    public decimal ExchangeRate { get; set; }
    public Dictionary<string, CurrencyMetric> CurrencyDistribution { get; set; }
}

public class StockMetric
{
    public string StockId { get; set; }
    public string StockName { get; set; }
    public decimal Quantity { get; set; }
    public decimal OriginalPrice { get; set; }  // 原始價格（未轉換）
    public decimal CurrentPrice { get; set; }   // 轉換後價格
    public decimal CurrentValue { get; set; }   // 轉換後總值
    public decimal OriginalValue { get; set; }  // 原始總值（未轉換）
    public decimal PercentageOfPortfolio { get; set; }
    public string Currency { get; set; }
    public decimal? ExchangeRate { get; set; }  // 如果有匯率轉換則提供
    public string ConvertedToCurrency { get; set; }  // 如果有匯率轉換則提供
}

public class CurrencyMetric
{
    public decimal TotalValue { get; set; }
    public decimal Percentage { get; set; }
}