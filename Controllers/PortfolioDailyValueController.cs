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
    private static DateTimeRange GetDateRange(TimeRange range)
    {
        // 將結束日期設為明天的午夜，確保包含今天所有時間的資料
        var endDate = DateTime.UtcNow.Date.AddDays(1);
    
        var startDate = range switch
        {
            TimeRange.OneMonth => endDate.AddMonths(-1),
            TimeRange.ThreeMonths => endDate.AddMonths(-3),
            TimeRange.SixMonths => endDate.AddMonths(-6),
            TimeRange.OneYear => endDate.AddYears(-1),
            _ => endDate.AddMonths(-1)
        };
    
        return new DateTimeRange(startDate, endDate);
    }


    /// <summary>
    /// 建立MongoDB過濾器的輔助方法
    /// </summary>
    private static FilterDefinition<PortfolioDailyValue> CreateDateRangeFilter(
        string portfolioId,
        DateTimeRange dateRange)
    {
        return Builders<PortfolioDailyValue>.Filter.And(
            Builders<PortfolioDailyValue>.Filter.Eq(x => x.PortfolioId, portfolioId),
            Builders<PortfolioDailyValue>.Filter.Gte(x => x.Date, dateRange.StartDate),
            Builders<PortfolioDailyValue>.Filter.Lt(x => x.Date, dateRange.EndDate)
        );
    }

    [HttpGet("{portfolioId}/history")]
    public async Task<ActionResult<PortfolioDailyValueResponse>> GetPortfolioHistory(
        [FromRoute] string portfolioId,
        [FromQuery] TimeRange range = TimeRange.OneMonth,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dateRange = GetDateRange(range);

            logger.LogInformation(
                "Fetching portfolio history for {PortfolioId} from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                portfolioId, dateRange.StartDate, dateRange.EndDate);

            var filter = CreateDateRangeFilter(portfolioId, dateRange);
            var sort = Builders<PortfolioDailyValue>.Sort.Ascending(x => x.Date);

            var dailyValues = await mongoDbService.PortfolioDailyValues
                .Find(filter)
                .Sort(sort)
                .Project(x => new DailyValueData 
                { 
                    Date = x.Date, 
                    TotalValueTwd = x.TotalValueTwd 
                })
                .ToListAsync(cancellationToken);

            if (!dailyValues.Any())
            {
                return NotFound($"No data found for portfolio {portfolioId} in the specified date range");
            }

            var summary = ValueSummary.Calculate(dailyValues);
            var response = new PortfolioDailyValueResponse(portfolioId, dailyValues, summary);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, 
                "Error fetching portfolio history for {PortfolioId}: {ErrorMessage}", 
                portfolioId, ex.Message);
            return StatusCode(500, "An error occurred while fetching portfolio history");
        }
    }

    [HttpGet("{portfolioId}/summary")]
    public async Task<ActionResult<ValueSummary>> GetPortfolioSummary(
        [FromRoute] string portfolioId,
        [FromQuery] TimeRange range = TimeRange.OneMonth,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dateRange = GetDateRange(range);
            var filter = CreateDateRangeFilter(portfolioId, dateRange);
            var sort = Builders<PortfolioDailyValue>.Sort.Ascending(x => x.Date);

            var dailyValues = await mongoDbService.PortfolioDailyValues
                .Find(filter)
                .Sort(sort)
                .Project(x => new DailyValueData 
                { 
                    Date = x.Date, 
                    TotalValueTwd = x.TotalValueTwd 
                })
                .ToListAsync(cancellationToken);

            if (!dailyValues.Any())
            {
                return NotFound($"No data found for portfolio {portfolioId}");
            }

            var summary = ValueSummary.Calculate(dailyValues);

            logger.LogInformation(
                """
                Portfolio summary for {PortfolioId}:
                Start Value: {StartValue:N0}
                End Value: {EndValue:N0}
                Change: {ChangePercentage:N2}%
                """,
                portfolioId,
                summary.StartValue,
                summary.EndValue,
                summary.ChangePercentage);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, 
                "Error fetching portfolio summary for {PortfolioId}: {ErrorMessage}", 
                portfolioId, ex.Message);
            return StatusCode(500, "An error occurred while fetching portfolio summary");
        }
    }
}

/// <summary>
/// 表示日期範圍的記錄類型
/// </summary>
public record DateTimeRange(DateTime StartDate, DateTime EndDate);
