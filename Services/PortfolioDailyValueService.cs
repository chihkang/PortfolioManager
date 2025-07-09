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
        
            // 建立有時區資訊的台北時間午夜
            var localMidnight = new DateTime(taipeiTime.Year, taipeiTime.Month, taipeiTime.Day, 0, 0, 0, DateTimeKind.Unspecified);
        
            // 將台北時間午夜轉換為 UTC 時間存儲
            var utcMidnight = TimeZoneInfo.ConvertTimeToUtc(localMidnight, taipeiTimeZone);

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
                        Date = utcMidnight, // 使用 UTC 時間
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
                        utcMidnight,
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