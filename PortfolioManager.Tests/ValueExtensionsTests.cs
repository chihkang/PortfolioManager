using PortfolioManager.Extensions;
using PortfolioManager.Models.Entities;
using Xunit;

namespace PortfolioManager.Tests;

public sealed class ValueExtensionsTests
{
    [Fact]
    public void ToDailyValueData_Maps_Date_And_Value()
    {
        var entity = new PortfolioDailyValue
        {
            Date = new DateTime(2025, 12, 17),
            TotalValueTwd = 9876.5m
        };

        var dto = entity.ToDailyValueData();

        Assert.Equal(entity.Date, dto.Date);
        Assert.Equal(entity.TotalValueTwd, dto.TotalValueTwd);
    }
}
