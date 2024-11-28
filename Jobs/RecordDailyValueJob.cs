using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Controllers;
using PortfolioManager.Services;
using Quartz;

namespace PortfolioManager.Jobs;

public class RecordDailyValueJob(
    PortfolioDailyValueService portfolioDailyValueService,
    ILogger<RecordDailyValueJob> logger,
    IExchangeRateService exchangeRateService)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var exchangeRate = await exchangeRateService.GetExchangeRateAsync();
            logger.LogInformation("Successfully fetched exchange rate: {Rate}", exchangeRate);

            await portfolioDailyValueService.RecordDailyValuesAsync(exchangeRate);
            logger.LogInformation("Successfully recorded daily values");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing daily value recording job");
        }
    }
}