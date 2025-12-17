using PortfolioManager.Models.DTO.PortfolioDailyValue;
using Xunit;

namespace PortfolioManager.Tests;

public sealed class PortfolioDailyValueResponseTests
{
    [Fact]
    public void DailyValueData_Create_Validates_Date_And_Value()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyValueData.Create(new DateTime(1999, 12, 31), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DailyValueData.Create(new DateTime(2000, 1, 1), -1));

        var ok = DailyValueData.Create(new DateTime(2025, 1, 2), 123.45m);
        Assert.Equal(new DateTime(2025, 1, 2), ok.Date);
        Assert.Equal(123.45m, ok.TotalValueTwd);
    }

    [Fact]
    public void ValueSummary_Calculate_Throws_On_Empty()
    {
        Assert.Throws<ArgumentException>(() => ValueSummary.Calculate(Array.Empty<DailyValueData>()));
    }

    [Fact]
    public void ValueSummary_Calculate_Computes_Min_Max_And_ChangePercentage()
    {
        var values = new[]
        {
            DailyValueData.Create(new DateTime(2025, 1, 2), 110m),
            DailyValueData.Create(new DateTime(2025, 1, 1), 100m),
            DailyValueData.Create(new DateTime(2025, 1, 3), 90m),
        };

        var summary = ValueSummary.Calculate(values);

        Assert.Equal(110m, summary.HighestValue);
        Assert.Equal(new DateTime(2025, 1, 2), summary.HighestValueDate);
        Assert.Equal(90m, summary.LowestValue);
        Assert.Equal(new DateTime(2025, 1, 3), summary.LowestValueDate);

        Assert.Equal(100m, summary.StartValue);
        Assert.Equal(90m, summary.EndValue);
        Assert.Equal(-10.00m, summary.ChangePercentage);
    }

    [Fact]
    public void HasBetterPerformanceThan_Uses_ChangePercentage()
    {
        var better = new ValueSummary { ChangePercentage = 1m };
        var worse = new ValueSummary { ChangePercentage = 0m };

        Assert.True(better.HasBetterPerformanceThan(worse));
        Assert.False(worse.HasBetterPerformanceThan(better));
    }
}
