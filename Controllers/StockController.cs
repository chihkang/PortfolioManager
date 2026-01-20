using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;
using PortfolioManager.Models;
using PortfolioManager.Models.Entities;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController(
    MongoDbService mongoDbService,
    ILogger<StockController> logger,
    IMediator mediator,
    IMemoryCache cache)
    : ControllerBase
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    public IMediator Mediator { get; } = mediator;

    /// <summary>
    ///     Get all stocks with minimal information (ID, Name, and Alias)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<StockListItemResponse>>> GetAllStocks()
    {
        try
        {
            const string cacheKey = "all_stocks_list";

            // Try to get from cache first
            if (cache.TryGetValue(cacheKey, out IEnumerable<StockListItemResponse>? cachedStocks))
            {
                logger.LogInformation("Returning cached stock list");
                return Ok(cachedStocks);
            }

            // If not in cache, get from database with projection
            var stocks = await mongoDbService.Stocks
                .Find(Builders<Stock>.Filter.Empty)
                .Project(Builders<Stock>.Projection
                    .Expression(s => new StockListItemResponse
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Alias = s.Alias
                    }))
                .ToListAsync();

            // Cache the result
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheDuration);

            cache.Set(cacheKey, stocks, cacheEntryOptions);

            logger.LogInformation("Retrieved {Count} stocks from database", stocks.Count);
            return Ok(stocks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving stock list");
            return StatusCode(500, "An error occurred while retrieving the stock list");
        }
    }

    /// <summary>
    ///     Get stock price by stock id
    /// </summary>
    [HttpGet("{id}/price")]
    public async Task<ActionResult<StockPriceInfo>> GetStockPriceById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("Stock id is required");

        if (!ObjectId.TryParse(id, out _)) return BadRequest("Invalid stock id format");

        try
        {
            var cacheKey = $"stock_price_{id}";

            if (cache.TryGetValue(cacheKey, out StockPriceInfo? cachedStock))
            {
                logger.LogInformation("Returning cached stock price for id {Id}", id);
                return Ok(cachedStock);
            }

            var stock = await mongoDbService.Stocks
                .Find(s => s != null && s.Id == id)
                .Project(Builders<Stock>.Projection
                    .Expression(s => new StockPriceInfo
                    {
                        Id = s.Id,
                        Price = s.Price,
                        Currency = s.Currency,
                        LastUpdated = s.LastUpdated
                    }))
                .FirstOrDefaultAsync();

            if (stock == null)
            {
                logger.LogWarning("Stock not found by id: {Id}", id);
                return NotFound($"Stock with id {id} not found");
            }

            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(CacheDuration);
            cache.Set(cacheKey, stock, cacheEntryOptions);

            logger.LogInformation("Retrieved stock price by id {Id}", id);
            return Ok(stock);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving stock price by id {Id}", id);
            return StatusCode(500, "An error occurred while retrieving the stock price");
        }
    }

    /// <summary>
    ///     Update stock price by name (e.g., "2330:TPE") with optimized performance
    /// </summary>
    [HttpPut("name/{name}/price")]
    public async Task<IActionResult> UpdateStockPriceByName(string name, [FromBody] decimal newPrice)
    {
        if (string.IsNullOrWhiteSpace(name)) return BadRequest("Stock name is required");

        if (newPrice <= 0) return BadRequest("Price must be greater than 0");

        try
        {
            // Use projection to get only necessary fields
            var stock = await mongoDbService.Stocks
                .Find(s => s != null && s.Name == name)
                .Project(Builders<Stock>.Projection
                    .Expression(s => new StockPriceInfo
                    {
                        Id = s.Id,
                        Price = s.Price,
                        Currency = s.Currency,
                        LastUpdated = s.LastUpdated
                    }))
                .FirstOrDefaultAsync();

            if (stock == null)
            {
                logger.LogWarning("Stock not found: {Name}", name);
                return NotFound($"Stock with name {name} not found");
            }

            var oldPrice = stock.Price;
            var now = DateTime.UtcNow;

            UpdateDefinition<Stock> update = Builders<Stock>.Update
                .Set(s => s.Price, newPrice)
                .Set(s => s.LastUpdated, now);

            // Execute update
            var updateResult = await mongoDbService.Stocks
                .WithWriteConcern(WriteConcern.WMajority)
                .UpdateOneAsync(
                    Builders<Stock>.Filter.Eq(s => s.Name, name),
                    update);

            if (updateResult.ModifiedCount == 0)
            {
                logger.LogWarning("No changes made to stock: {Name}", name);
                return StatusCode(500, "No changes were made to the stock");
            }

            // Remove from cache if exists
            var cacheKey = $"stock_price_{name}";
            cache.Remove(cacheKey);

            // Also remove the all stocks list cache as it might contain old price
            cache.Remove("all_stocks_list");

            var response = new UpdateStockPriceResponse
            {
                Name = name,
                OldPrice = oldPrice,
                NewPrice = newPrice,
                Currency = stock.Currency,
                LastUpdated = now
            };

            logger.LogInformation(
                "Updated price for stock {Name}: {OldPrice} -> {NewPrice}",
                name, oldPrice, newPrice);

            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating price for stock {Name}", name);
            return StatusCode(500, "An error occurred while updating the stock price");
        }
    }
}