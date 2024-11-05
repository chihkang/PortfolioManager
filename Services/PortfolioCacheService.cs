using Microsoft.Extensions.Caching.Distributed;
using MongoDB.Driver;
using PortfolioManager.Models;

namespace PortfolioManager.Services;

public class PortfolioCacheService(
    IDistributedCache cache,
    MongoDbService mongoDbService,
    ILogger<PortfolioCacheService> logger)
{
    private readonly ILogger<PortfolioCacheService> _logger = logger;

    public async Task<Portfolio> GetPortfolioWithCurrentValues(string portfolioId)
    {
        var cacheKey = $"portfolio:{portfolioId}:values";
        
        // 嘗試從快取獲取
        var cachedPortfolio = await cache.GetAsync<Portfolio>(cacheKey);
        if (cachedPortfolio != null)
        {
            return cachedPortfolio;
        }

        // 如果快取中沒有，重新計算
        var portfolio = await mongoDbService.Portfolios
            .Find(p => p.Id == portfolioId)
            .FirstOrDefaultAsync();

        if (portfolio != null)
        {
            await UpdatePortfolioValues(portfolio);
            
            // 設置快取，有效期 5 分鐘
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            };
            
            await cache.SetAsync(cacheKey, portfolio, options);
        }

        return portfolio;
    }

    public async Task InvalidatePortfolioCache(string portfolioId)
    {
        await cache.RemoveAsync($"portfolio:{portfolioId}:values");
    }

    private async Task UpdatePortfolioValues(Portfolio portfolio)
    {
        var stockIds = portfolio.Stocks.Select(s => s.StockId).ToList();
        var stocks = await mongoDbService.Stocks
            .Find(s => stockIds.Contains(s.Id))
            .ToListAsync();

        decimal totalValue = 0;
        var stockPrices = stocks.ToDictionary(s => s.Id, s => s.Price);

        foreach (var stock in portfolio.Stocks)
        {
            if (stockPrices.TryGetValue(stock.StockId, out decimal price))
            {
                decimal stockValue = price * stock.Quantity;
                totalValue += stockValue;
            }
        }

        foreach (var stock in portfolio.Stocks)
        {
            if (stockPrices.TryGetValue(stock.StockId, out decimal price))
            {
                decimal stockValue = price * stock.Quantity;
                stock.PercentageOfTotal = totalValue > 0 
                    ? Math.Round((stockValue / totalValue) * 100, 2)
                    : 0;
            }
        }

        portfolio.TotalValue = totalValue;
        portfolio.LastUpdated = DateTime.UtcNow;
    }
}