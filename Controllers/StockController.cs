using MediatR;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Events;
using PortfolioManager.Models;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController(MongoDbService mongoDbService, ILogger<StockController> logger, IMediator mediator)
        : ControllerBase
    {
        /// <summary>
        /// Get all stocks
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Stock>>> GetStocks()
        {
            try
            {
                logger.LogInformation("Attempting to get all stocks");
                var stocks = await mongoDbService.Stocks.Find(_ => true).ToListAsync();
                
                if (!stocks.Any())
                {
                    logger.LogWarning("No stocks found in database");
                }
                else
                {
                    logger.LogInformation($"Found {stocks.Count} stocks");
                }
                
                return Ok(stocks);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while getting stocks");
                return StatusCode(500, "An error occurred while retrieving stocks");
            }
        }

        /// <summary>
        /// Get stock by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Stock>> GetStock(string id)
        {
            try
            {
                var stock = await mongoDbService.Stocks.Find(s => s.Id == id).FirstOrDefaultAsync();
                
                if (stock == null)
                {
                    logger.LogWarning($"Stock with ID {id} not found");
                    return NotFound();
                }

                return Ok(stock);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error occurred while getting stock with ID {id}");
                return StatusCode(500, "An error occurred while retrieving the stock");
            }
        }

        /// <summary>
        /// Create a new stock
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Stock>> CreateStock(Stock stock)
        {
            try
            {
                stock.LastUpdated = DateTime.UtcNow;
                await mongoDbService.Stocks.InsertOneAsync(stock);
                logger.LogInformation($"Created new stock: {stock.Name}");
                return CreatedAtAction(nameof(GetStock), new { id = stock.Id }, stock);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while creating stock");
                return StatusCode(500, "An error occurred while creating the stock");
            }
        }

        /// <summary>
        /// Update stock information
        /// </summary>
        [HttpPut("{id}/price")]
        public async Task<IActionResult> UpdateStockPrice(string id, [FromBody] decimal newPrice)
        {
            try
            {
                var stock = await mongoDbService.Stocks
                    .Find(s => s.Id == id)
                    .FirstOrDefaultAsync();

                if (stock == null)
                    return NotFound();

                var oldPrice = stock.Price;

                // 更新股票價格
                var update = Builders<Stock>.Update
                    .Set(s => s.Price, newPrice)
                    .Set(s => s.LastUpdated, DateTime.UtcNow);

                await mongoDbService.Stocks.UpdateOneAsync(s => s.Id == id, update);

                // 發布價格更新事件
                await mediator.Publish(new StockPriceUpdatedEvent
                {
                    StockId = id,
                    OldPrice = oldPrice,
                    NewPrice = newPrice,
                    UpdatedAt = DateTime.UtcNow
                });

                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error updating price for stock {id}");
                return StatusCode(500, "An error occurred while updating the stock price");
            }
        }

        /// <summary>
        /// Delete stock
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteStock(string id)
        {
            try
            {
                var result = await mongoDbService.Stocks.DeleteOneAsync(s => s.Id == id);
                
                if (result.DeletedCount == 0)
                {
                    logger.LogWarning($"Stock with ID {id} not found for deletion");
                    return NotFound();
                }

                logger.LogInformation($"Deleted stock with ID: {id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error occurred while deleting stock with ID {id}");
                return StatusCode(500, "An error occurred while deleting the stock");
            }
        }
        
        /// <summary>
        /// Update stock price by name (e.g., "2330:TPE")
        /// </summary>
        [HttpPut("name/{name}/price")]
        public async Task<IActionResult> UpdateStockPriceByName(string name, [FromBody] decimal newPrice)
        {
            try
            {
                logger.LogInformation($"Attempting to update price for stock with name: {name}");

                // 查找股票
                var stock = await mongoDbService.Stocks
                    .Find(s => s.Name == name)
                    .FirstOrDefaultAsync();

                if (stock == null)
                {
                    logger.LogWarning($"Stock with name {name} not found");
                    return NotFound($"Stock with name {name} not found");
                }

                var oldPrice = stock.Price;

                // 檢查新價格是否有效
                if (newPrice <= 0)
                {
                    logger.LogWarning($"Invalid price {newPrice} provided for stock {name}");
                    return BadRequest("Price must be greater than 0");
                }

                // 更新股票價格
                var update = Builders<Stock>.Update
                    .Set(s => s.Price, newPrice)
                    .Set(s => s.LastUpdated, DateTime.UtcNow);

                var updateResult = await mongoDbService.Stocks
                    .UpdateOneAsync(s => s.Name == name, update);

                if (updateResult.ModifiedCount == 0)
                {
                    logger.LogWarning($"No changes were made to stock {name}");
                    return StatusCode(500, "No changes were made to the stock");
                }

                // 發布價格更新事件
                await mediator.Publish(new StockPriceUpdatedEvent
                {
                    StockId = stock.Id,
                    OldPrice = oldPrice,
                    NewPrice = newPrice,
                    UpdatedAt = DateTime.UtcNow
                });

                logger.LogInformation(
                    $"Successfully updated price for stock {name} from {oldPrice} to {newPrice}"
                );

                return Ok(new
                {
                    Name = name,
                    OldPrice = oldPrice,
                    NewPrice = newPrice,
                    Currency = stock.Currency,
                    LastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error updating price for stock {name}");
                return StatusCode(500, "An error occurred while updating the stock price");
            }
        }

        /// <summary>
        /// Update stock price by name with request body
        /// </summary>
        [HttpPut("price")]
        public async Task<IActionResult> UpdateStockPriceByNameInBody([FromBody] UpdateStockPriceRequest request)
        {
            try
            {
                logger.LogInformation($"Attempting to update price for stock with name: {request.Name}");

                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest("Stock name is required");
                }

                // 查找股票
                var stock = await mongoDbService.Stocks
                    .Find(s => s.Name == request.Name)
                    .FirstOrDefaultAsync();

                if (stock == null)
                {
                    logger.LogWarning($"Stock with name {request.Name} not found");
                    return NotFound($"Stock with name {request.Name} not found");
                }

                var oldPrice = stock.Price;

                // 檢查新價格是否有效
                if (request.NewPrice <= 0)
                {
                    logger.LogWarning($"Invalid price {request.NewPrice} provided for stock {request.Name}");
                    return BadRequest("Price must be greater than 0");
                }

                // 更新股票價格
                var update = Builders<Stock>.Update
                    .Set(s => s.Price, request.NewPrice)
                    .Set(s => s.LastUpdated, DateTime.UtcNow);

                var updateResult = await mongoDbService.Stocks
                    .UpdateOneAsync(s => s.Name == request.Name, update);

                if (updateResult.ModifiedCount == 0)
                {
                    logger.LogWarning($"No changes were made to stock {request.Name}");
                    return StatusCode(500, "No changes were made to the stock");
                }

                // 發布價格更新事件
                await mediator.Publish(new StockPriceUpdatedEvent
                {
                    StockId = stock.Id,
                    OldPrice = oldPrice,
                    NewPrice = request.NewPrice,
                    UpdatedAt = DateTime.UtcNow
                });

                logger.LogInformation(
                    $"Successfully updated price for stock {request.Name} from {oldPrice} to {request.NewPrice}"
                );

                return Ok(new
                {
                    Name = request.Name,
                    OldPrice = oldPrice,
                    NewPrice = request.NewPrice,
                    Currency = stock.Currency,
                    LastUpdated = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error updating price for stock {request.Name}");
                return StatusCode(500, "An error occurred while updating the stock price");
            }
        }
    }

    public class UpdateStockPriceRequest
    {
        public string Name { get; set; }
        public decimal NewPrice { get; set; }
    }
        
    
}