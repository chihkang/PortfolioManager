namespace PortfolioManager.Jobs;

public class RecordDailyValueJob(
    PortfolioDailyValueService portfolioDailyValueService,
    ILogger<RecordDailyValueJob> logger,
    IExchangeRateService exchangeRateService)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var jobStartTime = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("""
                Job execution started:
                Trigger: {TriggerName}
                Scheduled Fire Time: {ScheduledTime}
                Previous Fire Time: {PreviousFireTime}
                Next Fire Time: {NextFireTime}
                """,
                context.Trigger.Key.Name,
                context.ScheduledFireTimeUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                context.PreviousFireTimeUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                context.NextFireTimeUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            // 獲取匯率
            logger.LogInformation("Fetching exchange rate...");
            var exchangeRateStopwatch = Stopwatch.StartNew();
            var exchangeRate = await exchangeRateService.GetExchangeRateAsync();
            exchangeRateStopwatch.Stop();

            logger.LogInformation("""
                Exchange rate fetched successfully:
                Rate: {Rate}
                Duration: {Duration}ms
                """,
                exchangeRate,
                exchangeRateStopwatch.ElapsedMilliseconds);

            // 記錄每日價值
            logger.LogInformation("Starting daily values recording...");
            var recordingStopwatch = Stopwatch.StartNew();
            await portfolioDailyValueService.RecordDailyValuesAsync(exchangeRate);
            recordingStopwatch.Stop();

            // 記錄成功完成
            stopwatch.Stop();
            logger.LogInformation("""
                Job completed successfully:
                Total Duration: {TotalDuration}ms
                Exchange Rate Fetch Duration: {ExchangeRateDuration}ms
                Recording Duration: {RecordingDuration}ms
                Job Start Time: {StartTime}
                Job End Time: {EndTime}
                """,
                stopwatch.ElapsedMilliseconds,
                exchangeRateStopwatch.ElapsedMilliseconds,
                recordingStopwatch.ElapsedMilliseconds,
                jobStartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, """
                Job execution failed:
                Duration: {Duration}ms
                Error Type: {ErrorType}
                Error Message: {ErrorMessage}
                Stack Trace: {StackTrace}
                Job Start Time: {StartTime}
                Job End Time: {EndTime}
                """,
                stopwatch.ElapsedMilliseconds,
                ex.GetType().Name,
                ex.Message,
                ex.StackTrace,
                jobStartTime.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                DateTime.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            // 重新拋出異常，讓 Quartz 知道作業失敗
            throw;
        }
    }
}