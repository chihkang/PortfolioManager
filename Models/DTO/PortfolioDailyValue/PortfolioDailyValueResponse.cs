namespace PortfolioManager.Models.DTO.PortfolioDailyValue;

public sealed record PortfolioDailyValueResponse(
    string PortfolioId,
    IReadOnlyList<DailyValueData> Values,
    ValueSummary Summary);

public sealed record DailyValueData
{
    public required DateTime Date { get; init; }
    public required decimal TotalValueTwd { get; init; }

    private static readonly DateTime MinValidDate = new(2000, 1, 1);

    // 驗證邏輯
    public static DailyValueData Create(DateTime date, decimal totalValueTwd)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(date, MinValidDate);
        ArgumentOutOfRangeException.ThrowIfLessThan(totalValueTwd, 0);

        return new DailyValueData
        {
            Date = date,
            TotalValueTwd = totalValueTwd
        };
    }
}

public sealed record ValueSummary
{
    // 移除 private 建構函式，允許外部建立實例
    public ValueSummary()
    {
    }

    public decimal HighestValue { get; init; }
    public DateTime HighestValueDate { get; init; }
    public decimal LowestValue { get; init; }
    public DateTime LowestValueDate { get; init; }
    public decimal StartValue { get; init; }
    public decimal EndValue { get; init; }
    public decimal ChangePercentage { get; init; } // 改為可初始化的屬性

    // 保留工廠方法用於計算
    public static ValueSummary Calculate(IReadOnlyList<DailyValueData> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (!values.Any()) throw new ArgumentException("Values cannot be empty", nameof(values));

        var orderedValues = values.OrderBy(v => v.Date).ToList();
        var maxValue = values.MaxBy(v => v.TotalValueTwd)!;
        var minValue = values.MinBy(v => v.TotalValueTwd)!;

        var startValue = orderedValues.First().TotalValueTwd;
        var endValue = orderedValues.Last().TotalValueTwd;
        var changePercentage = startValue == 0 ? 0 : Math.Round((endValue - startValue) / startValue * 100, 2);

        return new ValueSummary
        {
            HighestValue = maxValue.TotalValueTwd,
            HighestValueDate = maxValue.Date,
            LowestValue = minValue.TotalValueTwd,
            LowestValueDate = minValue.Date,
            StartValue = startValue,
            EndValue = endValue,
            ChangePercentage = changePercentage
        };
    }

    public bool HasBetterPerformanceThan(ValueSummary other) =>
        this.ChangePercentage > other.ChangePercentage;
}

// 擴展方法
public static class PortfolioDailyValueExtensions
{
    public static PortfolioDailyValueResponse ToResponse(
        this IReadOnlyList<DailyValueData> values,
        string portfolioId)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(portfolioId);

        var valuesList = values.ToList().AsReadOnly();

        return new PortfolioDailyValueResponse(portfolioId, values, ValueSummary.Calculate(valuesList));
    }
}