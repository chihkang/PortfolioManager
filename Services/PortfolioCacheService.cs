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
        if (cachedPortfolio != null) return cachedPortfolio;

        // 如果快取中沒有，重新計算
        var portfolio = await mongoDbService.Portfolios
            .Find(p => p.Id == portfolioId)
            .FirstOrDefaultAsync();

        if (portfolio != null)
        {
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
}