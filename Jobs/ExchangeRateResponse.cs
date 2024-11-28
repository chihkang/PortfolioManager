namespace PortfolioManager.Jobs;

public record ExchangeRateResponse
{
    public string? CurrencyPair { get; init; }
    public decimal ExchangeRate { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}