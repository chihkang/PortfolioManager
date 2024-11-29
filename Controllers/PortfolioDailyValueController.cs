using System.Runtime.InteropServices;

namespace PortfolioManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioDailyValueController(
    MongoDbService mongoDbService,
    ILogger<PortfolioDailyValueController> logger)
    : ControllerBase
{
    /// <summary>
    /// 計算日期範圍的輔助方法
    /// </summary>
    private static (DateTime EndDate, DateTime StartDate) CalculateDateRange(TimeRange range)
    {
        var endDate = DateTime.UtcNow.Date;
        var startDate = range switch
        {
            TimeRange.OneMonth => endDate.AddMonths(-1),
            TimeRange.ThreeMonths => endDate.AddMonths(-3),
            TimeRange.SixMonths => endDate.AddMonths(-6),
            TimeRange.OneYear => endDate.AddYears(-1),
            _ => endDate.AddMonths(-1)
        };
        return (endDate, startDate);
    }

    /// <summary>
    /// 使用 Span 優化的集合轉換方法
    /// </summary>
    private static List<DailyValueData> ConvertToDailyValues(List<PortfolioDailyValue> values) // 明確指定為 List<T>
    {
        ReadOnlySpan<PortfolioDailyValue> valuesSpan = CollectionsMarshal.AsSpan(values);
        var dailyValues = new List<DailyValueData>(valuesSpan.Length);

        for (int i = 0; i < valuesSpan.Length; i++)
        {
            ref readonly var value = ref valuesSpan[i];
            dailyValues.Add(new DailyValueData
            {
                Date = value.Date,
                TotalValueTwd = value.TotalValueTwd
            });
        }

        return dailyValues;
    }

    /// <summary>
    /// 建立MongoDB過濾器的輔助方法
    /// </summary>
    private static FilterDefinition<PortfolioDailyValue> CreateDateRangeFilter(
        ref readonly string portfolioId,
        DateTime startDate,
        DateTime endDate)
    {
        FilterDefinition<PortfolioDailyValue>[] filters =
        [
            Builders<PortfolioDailyValue>.Filter.Eq(x => x.PortfolioId, portfolioId),
            Builders<PortfolioDailyValue>.Filter.Gte(x => x.Date, startDate),
            Builders<PortfolioDailyValue>.Filter.Lte(x => x.Date, endDate)
        ];

        return Builders<PortfolioDailyValue>.Filter.And(filters);
    }

    [HttpGet("{portfolioId}/history")]
    public async Task<ActionResult<PortfolioDailyValueResponse>> GetPortfolioHistory(
        [FromRoute] string portfolioId,
        [FromQuery] TimeRange range = TimeRange.OneMonth)
    {
        try
        {
            var (endDate, startDate) = CalculateDateRange(range);

            logger.LogInformation("""
                                  Fetching portfolio history:
                                  Portfolio ID: {portfolioId}
                                  Date Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}
                                  """,
                portfolioId, startDate, endDate);

            var filter = CreateDateRangeFilter(ref portfolioId, startDate, endDate);
            var sort = Builders<PortfolioDailyValue>.Sort.Ascending(x => x.Date);

            var values = await mongoDbService.PortfolioDailyValues
                .Find(filter)
                .Sort(sort)
                .ToListAsync();

            if (values is not [_, ..]) // 使用 list pattern 檢查是否為空
            {
                return NotFound($"No data found for portfolio {portfolioId} in the specified date range");
            }

            var dailyValues = ConvertToDailyValues(values);

            var summary = ValueSummary.Calculate(dailyValues);

            var response = new PortfolioDailyValueResponse
            {
                PortfolioId = portfolioId,
                Values = dailyValues,
                Summary = summary
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, """
                                Error fetching portfolio history:
                                Portfolio ID: {portfolioId}
                                Error: {message}
                                """,
                portfolioId, ex.Message);
            return StatusCode(500, "An error occurred while fetching portfolio history");
        }
    }

    [HttpGet("{portfolioId}/summary")]
    public async Task<ActionResult<ValueSummary>> GetPortfolioSummary(
        [FromRoute] string portfolioId,
        [FromQuery] TimeRange range = TimeRange.OneMonth)
    {
        try
        {
            var (endDate, startDate) = CalculateDateRange(range);
            var filter = CreateDateRangeFilter(ref portfolioId, startDate, endDate);
            var sort = Builders<PortfolioDailyValue>.Sort.Ascending(x => x.Date);

            var dailyValues = await mongoDbService.PortfolioDailyValues
                .Find(filter)
                .Sort(sort)
                .ToListAsync();

            if (dailyValues is not [_, ..])
            {
                return NotFound($"No data found for portfolio {portfolioId}");
            }

            var values = ConvertToDailyValues(dailyValues);

            var summary = ValueSummary.Calculate(values);

            logger.LogInformation("""
                                  Successfully fetched summary for portfolio {portfolioId}:
                                  Start Value: {startValue:N0}
                                  End Value: {endValue:N0}
                                  Change: {changePercentage:N2}%
                                  """,
                portfolioId,
                summary.StartValue,
                summary.EndValue,
                summary.ChangePercentage);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, """
                                Error fetching portfolio summary:
                                Portfolio ID: {portfolioId}
                                Error: {message}
                                """,
                portfolioId, ex.Message);
            return StatusCode(500, "An error occurred while fetching portfolio history");
        }
    }
}