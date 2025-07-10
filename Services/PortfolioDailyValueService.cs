namespace PortfolioManager.Services;

public class PortfolioDailyValueService(
    MongoDbService mongoDbService,
    ILogger<PortfolioDailyValueService> logger)
{
    public async Task RecordDailyValuesAsync(decimal exchangeRate)
    {
        try
        {
            var taipeiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
            var taipeiTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, taipeiTimeZone);
        
            // 重要：使用台北時間的日期，但存儲為 UTC 的同一天午夜
            // 這樣前端轉換後日期才會正確
            var dateForStorage = new DateTime(
                taipeiTime.Year, 
                taipeiTime.Month, 
                taipeiTime.Day, 
                0, 0, 0, 
                DateTimeKind.Utc
            );

            var portfolios = await mongoDbService.Portfolios
                .Find(_ => true)
                .ToListAsync();

            foreach (var portfolio in portfolios)
            {
                try
                {
                    var totalValueTwd = await CalculatePortfolioValueAsync(portfolio, exchangeRate);

                    var dailyValue = new PortfolioDailyValue
                    {
                        PortfolioId = portfolio.Id,
                        Date = dateForStorage, // 存儲為 2025-07-09T00:00:00.000Z
                        TotalValueTwd = totalValueTwd
                    };

                    await mongoDbService.PortfolioDailyValues.InsertOneAsync(dailyValue);
                
                    logger.LogInformation("""
                                          Recorded daily value for portfolio {PortfolioId}:
                                          Taipei Date: {TaipeiDate:yyyy-MM-dd}
                                          UTC Storage Time: {UtcTime:yyyy-MM-dd HH:mm:ss}
                                          Value: {Value:N0} TWD
                                          """,
                        portfolio.Id,
                        taipeiTime.Date,
                        dateForStorage,
                        totalValueTwd);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error recording daily value for portfolio {PortfolioId}", portfolio.Id);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recording daily values");
            throw;
        }
    }




    private async Task<decimal> CalculatePortfolioValueAsync(Portfolio portfolio, decimal exchangeRate)
    {
        if (portfolio.Stocks.Count == 0)
            return 0;

        var stockIds = portfolio.Stocks.Select(s => s.StockId).ToList();
        var stocks = await mongoDbService.Stocks
            .Find(s => stockIds.Contains(s.Id))
            .ToListAsync();

        return portfolio.Stocks.Sum(portfolioStock =>
        {
            var stockDetails = stocks.FirstOrDefault(s => s.Id == portfolioStock.StockId);
            if (stockDetails == null)
                return 0;

            return stockDetails.Currency == "USD"
                ? portfolioStock.Quantity * stockDetails.Price * exchangeRate
                : portfolioStock.Quantity * stockDetails.Price;
        });
    }
}