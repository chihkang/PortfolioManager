using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Models;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioDailyValueController(
    MongoDbService mongoDbService,
    ILogger<PortfolioDailyValueController> logger)
    : ControllerBase
{
    [HttpGet("{portfolioId}/history")]
    public async Task<ActionResult<PortfolioDailyValueResponse>> GetPortfolioHistory(
        string portfolioId,
        [FromQuery] TimeRange range = TimeRange.OneMonth)
    {
        try
        {
            // 計算日期範圍
            var endDate = DateTime.UtcNow.Date;
            var startDate = range switch
            {
                TimeRange.OneMonth => endDate.AddMonths(-1),
                TimeRange.ThreeMonths => endDate.AddMonths(-3),
                TimeRange.SixMonths => endDate.AddMonths(-6),
                TimeRange.OneYear => endDate.AddYears(-1),
                _ => endDate.AddMonths(-1)
            };

            logger.LogInformation(
                $"Fetching portfolio history for {portfolioId} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

            // 建立查詢
            var filter = Builders<PortfolioDailyValue>.Filter.And(
                Builders<PortfolioDailyValue>.Filter.Eq(x => x.PortfolioId, portfolioId),
                Builders<PortfolioDailyValue>.Filter.Gte(x => x.Date, startDate),
                Builders<PortfolioDailyValue>.Filter.Lte(x => x.Date, endDate)
            );

            var sort = Builders<PortfolioDailyValue>.Sort.Ascending(x => x.Date);

            // 執行查詢
            var values = await mongoDbService.PortfolioDailyValues
                .Find(filter)
                .Sort(sort)
                .ToListAsync();

            if (!values.Any())
                return NotFound($"No data found for portfolio {portfolioId} in the specified date range");

            // 計算統計數據
            var summary = new ValueSummary
            {
                StartValue = values.First().TotalValueTWD,
                EndValue = values.Last().TotalValueTWD,
                HighestValue = values.Max(x => x.TotalValueTWD),
                LowestValue = values.Min(x => x.TotalValueTWD),
                HighestValueDate = values.First(x => x.TotalValueTWD == values.Max(v => v.TotalValueTWD)).Date,
                LowestValueDate = values.First(x => x.TotalValueTWD == values.Min(v => v.TotalValueTWD)).Date
            };

            // 計算漲跌幅
            summary.ChangePercentage = (summary.EndValue - summary.StartValue) / summary.StartValue * 100;

            var response = new PortfolioDailyValueResponse
            {
                PortfolioId = portfolioId,
                Values = values.Select(x => new DailyValueData
                {
                    Date = x.Date,
                    TotalValueTWD = x.TotalValueTWD
                }).ToList(),
                Summary = summary
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error fetching portfolio history for {portfolioId}");
            return StatusCode(500, "An error occurred while fetching portfolio history");
        }
    }

    [HttpGet("{portfolioId}/summary")]
    public async Task<ActionResult<ValueSummary>> GetPortfolioSummary(
        string portfolioId,
        [FromQuery] TimeRange range = TimeRange.OneMonth)
    {
        try
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

            var filter = Builders<PortfolioDailyValue>.Filter.And(
                Builders<PortfolioDailyValue>.Filter.Eq(x => x.PortfolioId, portfolioId),
                Builders<PortfolioDailyValue>.Filter.Gte(x => x.Date, startDate),
                Builders<PortfolioDailyValue>.Filter.Lte(x => x.Date, endDate)
            );

            // 使用 Builders 來建構聚合管道
            var sortByDate = Builders<PortfolioDailyValue>.Sort.Ascending(x => x.Date);

            // 先獲取所有符合條件的數據
            var values = await mongoDbService.PortfolioDailyValues
                .Find(filter)
                .Sort(sortByDate)
                .ToListAsync();

            if (!values.Any()) return NotFound($"No data found for portfolio {portfolioId}");

            // 計算摘要
            var summary = new ValueSummary
            {
                StartValue = values.First().TotalValueTWD,
                EndValue = values.Last().TotalValueTWD,
                HighestValue = values.Max(x => x.TotalValueTWD),
                LowestValue = values.Min(x => x.TotalValueTWD),
                HighestValueDate = values.OrderByDescending(x => x.TotalValueTWD).First().Date,
                LowestValueDate = values.OrderBy(x => x.TotalValueTWD).First().Date
            };

            // 計算漲跌幅
            if (summary.StartValue != 0)
                summary.ChangePercentage = Math.Round(
                    (summary.EndValue - summary.StartValue) / summary.StartValue * 100,
                    2
                );

            logger.LogInformation($"Successfully fetched summary for portfolio {portfolioId}: " +
                                  $"Start: {summary.StartValue:N0}, " +
                                  $"End: {summary.EndValue:N0}, " +
                                  $"Change: {summary.ChangePercentage:N2}%");

            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error fetching portfolio summary for {portfolioId}");
            return StatusCode(500, "An error occurred while fetching portfolio summary");
        }
    }
}