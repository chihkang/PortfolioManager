using MediatR;
using MongoDB.Driver;
using PortfolioManager.Events;
using PortfolioManager.Services;

namespace PortfolioManager.Handlers
{
    public class ExchangeRateUpdateHandler : INotificationHandler<ExchangeRateUpdatedEvent>
    {
        private readonly MongoDbService _mongoDbService;
        private readonly PortfolioCalculationService _calculationService;
        private readonly PortfolioCacheService _cacheService;
        private readonly ILogger<ExchangeRateUpdateHandler> _logger;

        public ExchangeRateUpdateHandler(
            MongoDbService mongoDbService,
            PortfolioCalculationService calculationService,
            PortfolioCacheService cacheService,
            ILogger<ExchangeRateUpdateHandler> logger)
        {
            _mongoDbService = mongoDbService;
            _calculationService = calculationService;
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task Handle(ExchangeRateUpdatedEvent notification, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation(
                    "Processing exchange rate update: Old Rate = {OldRate}, New Rate = {NewRate}",
                    notification.OldRate,
                    notification.NewRate
                );

                // 1. 找出所有 USD 股票
                var usdStocks = await _mongoDbService.Stocks
                    .Find(s => s.Currency == "USD")
                    .ToListAsync(cancellationToken);

                if (!usdStocks.Any())
                {
                    _logger.LogInformation("No USD stocks found");
                    return;
                }

                var usdStockIds = usdStocks.Select(s => s.Id).ToList();

                // 2. 找出包含 USD 股票的 Portfolio
                var portfolios = await _mongoDbService.Portfolios
                    .Find(p => 
                        p.ExchangeRate.HasValue && 
                        p.Stocks.Any(s => usdStockIds.Contains(s.StockId)))
                    .ToListAsync(cancellationToken);

                if (!portfolios.Any())
                {
                    _logger.LogInformation("No portfolios with USD stocks found");
                    return;
                }

                // 3. 獲取所有相關的股票價格（包括非 USD 股票）
                var allStockIds = portfolios
                    .SelectMany(p => p.Stocks.Select(s => s.StockId))
                    .Distinct()
                    .ToList();

                var allStocks = await _mongoDbService.Stocks
                    .Find(s => allStockIds.Contains(s.Id))
                    .ToListAsync(cancellationToken);

                var stockPrices = allStocks.ToDictionary(s => s.Id, s => s.Price);

                // 4. 更新每個 Portfolio
                foreach (var portfolio in portfolios)
                {
                    try
                    {
                        // 檢查 Portfolio 是否真的包含 USD 股票
                        var portfolioStockIds = portfolio.Stocks.Select(s => s.StockId).ToList();
                        var hasUsdStocks = usdStocks.Any(s => portfolioStockIds.Contains(s.Id));

                        if (!hasUsdStocks)
                        {
                            _logger.LogWarning(
                                "Portfolio {PortfolioId} was selected but doesn't contain USD stocks. Skipping.",
                                portfolio.Id
                            );
                            continue;
                        }

                        // 重新計算 Portfolio 價值
                        await _calculationService.UpdatePortfolioValues(portfolio, stockPrices);
                        
                        // 清除快取
                        await _cacheService.InvalidatePortfolioCache(portfolio.Id);

                        _logger.LogInformation(
                            "Updated portfolio {PortfolioId} with new exchange rate",
                            portfolio.Id
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, 
                            "Error updating portfolio {PortfolioId} during exchange rate update",
                            portfolio.Id
                        );
                    }
                }

                _logger.LogInformation(
                    "Completed exchange rate update for {Count} portfolios",
                    portfolios.Count
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing exchange rate update");
                throw;
            }
        }
    }
}