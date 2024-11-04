using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Models;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController(MongoDbService mongoDbService, ILogger<StockController> logger)
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
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStock(string id, Stock stock)
        {
            try
            {
                stock.LastUpdated = DateTime.UtcNow;
                var result = await mongoDbService.Stocks.ReplaceOneAsync(s => s.Id == id, stock);
                
                if (result.ModifiedCount == 0)
                {
                    logger.LogWarning($"Stock with ID {id} not found for update");
                    return NotFound();
                }

                logger.LogInformation($"Updated stock with ID: {id}");
                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error occurred while updating stock with ID {id}");
                return StatusCode(500, "An error occurred while updating the stock");
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
        
    }
}