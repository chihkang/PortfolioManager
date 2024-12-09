namespace PortfolioManager.Services;

public class PortfolioDailyValueService(
    MongoDbService mongoDbService,
    ILogger<PortfolioDailyValueService> logger)
{
    public async Task RecordDailyValuesAsync(decimal exchangeRate)
    {
        try
        {
            // 使用台北時區來計算正確的日期
            var taipeiTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
            var taipeiTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, taipeiTimeZone);
            // 直接使用台北當天的日期，不需要轉換回 UTC
            var localMidnight = new DateTime(taipeiTime.Year, taipeiTime.Month, taipeiTime.Day, 0, 0, 0);


            var portfolios = await mongoDbService.Portfolios
                .Find(_ => true)
                .ToListAsync();

            foreach (var portfolio in portfolios)
                try
                {
                    // 計算總資產價值
                    var totalValueTwd = await CalculatePortfolioValueAsync(portfolio, exchangeRate);

                    if (portfolio.Stocks.Any())
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
                        Date = localMidnight,
                        TotalValueTwd = totalValueTwd
                    };

                    await mongoDbService.PortfolioDailyValues.InsertOneAsync(dailyValue);
                    logger.LogInformation("""
                                          Recorded daily value for portfolio {PortfolioId}:
                                          Local Date: {LocalDate:yyyy-MM-dd}
                                          UTC Date: {UtcDate:yyyy-MM-dd HH:mm:ss}
                                          Value: {Value:N0} TWD
                                          """,
                        portfolio.Id,
                        taipeiTime.Date,
                        localMidnight,
                        totalValueTwd);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error recording daily value for portfolio {PortfolioId}", portfolio.Id);
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