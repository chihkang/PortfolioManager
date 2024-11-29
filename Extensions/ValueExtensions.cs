namespace PortfolioManager.Extensions;

public static class ValueExtensions 
{
    public static DailyValueData ToDailyValueData(this PortfolioDailyValue value) => 
        new()
        {
            Date = value.Date,
            TotalValueTwd = value.TotalValueTwd
        };
}