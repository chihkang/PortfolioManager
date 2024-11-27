using Microsoft.Extensions.Options;
using MongoDB.Driver;
using PortfolioManager.Configuration;
using PortfolioManager.Models;

namespace PortfolioManager.Services;

public class PortfolioUpdateService
{
    private readonly ILogger<PortfolioUpdateService> _logger;
    private readonly MongoDbService _mongoDbService;
    private readonly IOptions<PortfolioUpdateOptions> _options;

    public PortfolioUpdateService(
        MongoDbService mongoDbService,
        ILogger<PortfolioUpdateService> logger,
        IOptions<PortfolioUpdateOptions> options)
    {
        _mongoDbService = mongoDbService;
        _logger = logger;
        _options = options;
    }

    public async Task<List<string>> GetAffectedPortfolios(string stockId)
    {
        try
        {
            var filter = Builders<Portfolio>.Filter.ElemMatch(
                p => p.Stocks,
                stock => stock.StockId == stockId
            );

            var portfolios = await _mongoDbService.Portfolios
                .Find(filter)
                .Project(p => p.Id)
                .ToListAsync();

            _logger.LogInformation($"Found {portfolios.Count} portfolios containing stock {stockId}");
            return portfolios;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting affected portfolios for stock {stockId}");
            throw;
        }
    }
}