using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using PortfolioManager.Models;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly MongoDbService _mongoDbService;
        private readonly ILogger<StockController> _logger;
        private readonly IMediator _mediator;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public StockController(
            MongoDbService mongoDbService,
            ILogger<StockController> logger,
            IMediator mediator,
            IMemoryCache cache)
        {
            _mongoDbService = mongoDbService;
            _logger = logger;
            _mediator = mediator;
            _cache = cache;
        }

        /// <summary>
        /// Get all stocks with minimal information (ID, Name, and Alias)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<StockListItemResponse>>> GetAllStocks()
        {
            try
            {
                const string cacheKey = "all_stocks_list";

                // Try to get from cache first
                if (_cache.TryGetValue(cacheKey, out IEnumerable<StockListItemResponse>? cachedStocks))
                {
                    _logger.LogInformation("Returning cached stock list");
                    return Ok(cachedStocks);
                }

                // If not in cache, get from database with projection
                var stocks = await _mongoDbService.Stocks
                    .Find(Builders<Stock>.Filter.Empty)
                    .Project<StockListItemResponse>(Builders<Stock>.Projection
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
                
                _cache.Set(cacheKey, stocks, cacheEntryOptions);

                _logger.LogInformation("Retrieved {Count} stocks from database", stocks.Count);
                return Ok(stocks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving stock list");
                return StatusCode(500, "An error occurred while retrieving the stock list");
            }
        }

        /// <summary>
        /// Update stock price by name (e.g., "2330:TPE") with optimized performance
        /// </summary>
        [HttpPut("name/{name}/price")]
        public async Task<IActionResult> UpdateStockPriceByName(string name, [FromBody] decimal newPrice)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest("Stock name is required");
            }

            if (newPrice <= 0)
            {
                return BadRequest("Price must be greater than 0");
            }

            try
            {
                // Use projection to get only necessary fields
                var stock = await _mongoDbService.Stocks
                    .Find(s => s.Name == name)
                    .Project<StockPriceInfo>(Builders<Stock>.Projection
                        .Expression(s => new StockPriceInfo
                        {
                            Id = s.Id,
                            Price = s.Price,
                            Currency = s.Currency
                        }))
                    .FirstOrDefaultAsync();

                if (stock == null)
                {
                    _logger.LogWarning("Stock not found: {Name}", name);
                    return NotFound($"Stock with name {name} not found");
                }

                var oldPrice = stock.Price;
                var now = DateTime.UtcNow;

                // Prepare update definition outside of UpdateOneAsync
                UpdateDefinition<Stock> update = Builders<Stock>.Update
                    .Set(s => s.Price, newPrice)
                    .Set(s => s.LastUpdated, now);

                // Execute update
                var updateResult = await _mongoDbService.Stocks
                    .WithWriteConcern(WriteConcern.WMajority)
                    .UpdateOneAsync(
                        Builders<Stock>.Filter.Eq(s => s.Name, name),
                        update);

                if (updateResult.ModifiedCount == 0)
                {
                    _logger.LogWarning("No changes made to stock: {Name}", name);
                    return StatusCode(500, "No changes were made to the stock");
                }

                // Remove from cache if exists
                var cacheKey = $"stock_price_{name}";
                _cache.Remove(cacheKey);
                
                // Also remove the all stocks list cache as it might contain old price
                _cache.Remove("all_stocks_list");
                
                var response = new UpdateStockPriceResponse
                {
                    Name = name,
                    OldPrice = oldPrice,
                    NewPrice = newPrice,
                    Currency = stock.Currency,
                    LastUpdated = now
                };

                _logger.LogInformation(
                    "Updated price for stock {Name}: {OldPrice} -> {NewPrice}",
                    name, oldPrice, newPrice);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating price for stock {Name}", name);
                return StatusCode(500, "An error occurred while updating the stock price");
            }
        }
    }

    public class StockPriceInfo
    {
        public string Id { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; }
    }

    public class UpdateStockPriceResponse
    {
        public string Name { get; set; }
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public required string Currency { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class StockListItemResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Alias { get; set; }
    }
}