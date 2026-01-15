using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Models.Entities;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PositionEventController(MongoDbService mongoDbService, ILogger<PositionEventController> logger) : ControllerBase
{
    /// <summary>
    /// 取得所有交易紀錄（支援分頁）
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PositionEvent>>> GetPositionEvents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;
        var events = await mongoDbService.PositionEvents
            .Find(_ => true)
            .SortByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// 根據 ID 取得單一交易紀錄
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<PositionEvent>> GetPositionEvent(string id)
    {
        var positionEvent = await mongoDbService.PositionEvents
            .Find(e => e.Id == id)
            .FirstOrDefaultAsync();

        if (positionEvent == null)
            return NotFound();

        return Ok(positionEvent);
    }

    /// <summary>
    /// 根據使用者 ID 取得交易紀錄
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<PositionEvent>>> GetUserPositionEvents(
        string userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;
        var events = await mongoDbService.PositionEvents
            .Find(e => e.UserId == userId)
            .SortByDescending(e => e.TradeAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// 根據股票 ID 取得交易紀錄
    /// </summary>
    [HttpGet("stock/{stockId}")]
    public async Task<ActionResult<IEnumerable<PositionEvent>>> GetStockPositionEvents(
        string stockId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;
        var events = await mongoDbService.PositionEvents
            .Find(e => e.StockId == stockId)
            .SortByDescending(e => e.TradeAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// 根據使用者和股票 ID 取得交易紀錄
    /// </summary>
    [HttpGet("user/{userId}/stock/{stockId}")]
    public async Task<ActionResult<IEnumerable<PositionEvent>>> GetUserStockPositionEvents(
        string userId,
        string stockId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var skip = (page - 1) * pageSize;
        var events = await mongoDbService.PositionEvents
            .Find(e => e.UserId == userId && e.StockId == stockId)
            .SortByDescending(e => e.TradeAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// 檢查 OperationId 是否存在（防止重複操作）
    /// </summary>
    [HttpGet("check/{operationId}")]
    public async Task<ActionResult<bool>> CheckOperationExists(string operationId)
    {
        var exists = await mongoDbService.PositionEvents
            .Find(e => e.OperationId == operationId)
            .AnyAsync();

        return Ok(new { operationId, exists });
    }

    /// <summary>
    /// 取得交易統計
    /// </summary>
    [HttpGet("stats/user/{userId}")]
    public async Task<ActionResult> GetUserStats(string userId)
    {
        var events = await mongoDbService.PositionEvents
            .Find(e => e.UserId == userId)
            .ToListAsync();

        var stats = new
        {
            totalTransactions = events.Count,
            buyCount = events.Count(e => e.Type == "BUY"),
            sellCount = events.Count(e => e.Type == "SELL"),
            totalBuyVolume = events.Where(e => e.Type == "BUY").Sum(e => e.QuantityDelta),
            totalSellVolume = events.Where(e => e.Type == "SELL").Sum(e => Math.Abs(e.QuantityDelta)),
            currencies = events.Select(e => e.Currency).Distinct().ToList(),
            earliestTrade = events.Min(e => e.TradeAt),
            latestTrade = events.Max(e => e.TradeAt)
        };

        return Ok(stats);
    }
}
