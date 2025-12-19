using Quartz;

namespace PortfolioManager.Jobs;

public class StockUpdaterJob(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<StockUpdaterJob> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var dataMap = context.MergedJobDataMap;
        var stockType = dataMap.GetString("stockType");
        var baseUrl = configuration["StockUpdaterBaseUrl"];
        
        if (string.IsNullOrEmpty(baseUrl))
        {
            logger.LogError("StockUpdaterBaseUrl is not configured.");
            return;
        }

        var url = $"{baseUrl}/run?stockType={stockType}";

        logger.LogInformation("Starting StockUpdaterJob for {StockType} at {Url}", stockType, url);

        try
        {
            using var client = httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation("StockUpdaterJob for {StockType} completed successfully. Status: {StatusCode}", 
                    stockType, response.StatusCode);
            }
            else
            {
                logger.LogWarning("StockUpdaterJob for {StockType} failed. Status: {StatusCode}", 
                    stockType, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing StockUpdaterJob for {StockType}", stockType);
            // We might not want to throw here to avoid cluttering logs if the external service is down, 
            // but Quartz retry policies depend on it. For now, just log.
        }
    }
}
