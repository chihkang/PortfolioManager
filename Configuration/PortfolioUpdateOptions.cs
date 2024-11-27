namespace PortfolioManager.Configuration;

public class PortfolioUpdateOptions
{
    public int BatchSize { get; set; } = 100;
    public int UpdateIntervalMinutes { get; set; } = 5;
    public int CacheExpirationMinutes { get; set; } = 5;
    public int MaxRetryAttempts { get; set; } = 3;
}