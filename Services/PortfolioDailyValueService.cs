using MongoDB.Driver;
using PortfolioManager.Models;

namespace PortfolioManager.Services;

public class PortfolioDailyValueService(
    MongoDbService mongoDbService,
    ILogger<PortfolioDailyValueService> logger)
{
    public async Task RecordDailyValuesAsync(decimal exchangeRate)
    {
        try
        {
            var portfolios = await mongoDbService.Portfolios
                .Find(_ => true)
                .ToListAsync();

            foreach (var portfolio in portfolios)
            {
                try
                {
                    // 計算總資產價值
                    decimal totalValueTwd = 0;
                    
                    if (portfolio.Stocks.Any() == true)
                    {
                        var stockIds = portfolio.Stocks.Select(s => s.StockId).ToList();
                        var stocks = await mongoDbService.Stocks
                            .Find(s => stockIds.Contains(s.Id))
                            .ToListAsync();

                        foreach (var portfolioStock in portfolio.Stocks)
                        {
                            var stockDetails = stocks.FirstOrDefault(s => s.Id == portfolioStock.StockId);
                            if (stockDetails != null)
                            {
                                var value = stockDetails.Currency == "USD"
                                    ? portfolioStock.Quantity * stockDetails.Price * exchangeRate
                                    : portfolioStock.Quantity * stockDetails.Price;
                                
                                totalValueTwd += value;
                            }
                        }
                    }

                    var dailyValue = new PortfolioDailyValue
                    {
                        PortfolioId = portfolio.Id,
                        Date = DateTime.UtcNow.Date,
                        TotalValueTWD = totalValueTwd
                    };

                    await mongoDbService.PortfolioDailyValues.InsertOneAsync(dailyValue);
                    logger.LogInformation($"Recorded daily value for portfolio {portfolio.Id}: {totalValueTwd:N0} TWD");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, $"Error recording daily value for portfolio {portfolio.Id}");
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recording daily values");
            throw;
        }
    }
}