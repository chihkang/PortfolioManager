namespace PortfolioManager;

public interface IExchangeRateService
{
    Task<decimal> GetExchangeRateAsync();
}