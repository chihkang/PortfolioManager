using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Controllers;
using PortfolioManager.Services;
using Quartz;

namespace PortfolioManager.Jobs;

public class RecordDailyValueJob(
    PortfolioDailyValueService portfolioDailyValueService,
    ILogger<RecordDailyValueJob> logger,
    ExchangeRateController exchangeRateController)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            // 取得匯率
            var rateResult = await exchangeRateController.GetExchangeRate();
            decimal exchangeRate;

            if (rateResult is OkObjectResult { Value: ExchangeRateResponse rateResponse })
            {
                exchangeRate = Convert.ToDecimal(rateResponse.ExchangeRate);
                logger.LogInformation($"Successfully fetched exchange rate: {exchangeRate}");
            }
            else
            {
                logger.LogWarning("Exchange rate not available, using default rate 30.5");
                exchangeRate = 30.5m;
            }

            await portfolioDailyValueService.RecordDailyValuesAsync(exchangeRate);
            logger.LogInformation("Successfully recorded daily values");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing daily value recording job");
        }
    }
}
public abstract class ExchangeRateResponse
{
    public double ExchangeRate { get; set; }
}