using MongoDB.Driver;
using PortfolioManager.Models;

namespace PortfolioManager.Services;

public class PortfolioUpdateService(
    MongoDbService mongoDbService,
    ILogger<PortfolioUpdateService> logger)
{
    public async Task<List<string?>> GetAffectedPortfolios(string stockId)
    {
        try
        {
            var filter = Builders<Portfolio>.Filter.ElemMatch(
                p => p.Stocks,
                stock => stock.StockId == stockId
            );

            var portfolios = await mongoDbService.Portfolios
                .Find(filter)
                .Project(p => p.Id)
                .ToListAsync();

            logger.LogInformation($"Found {portfolios.Count} portfolios containing stock {stockId}");
            return portfolios;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error getting affected portfolios for stock {stockId}");
            throw;
        }
    }
}