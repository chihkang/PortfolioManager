using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Models;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PortfolioController(MongoDbService mongoDbService) : ControllerBase
    {
        /// <summary>
        /// Get portfolio by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Portfolio>> GetPortfolio(string id)
        {
            var portfolio = await mongoDbService.Portfolios.Find(p => p.Id == id).FirstOrDefaultAsync();
            if (portfolio == null)
                return NotFound();

            return Ok(portfolio);
        }

        /// <summary>
        /// Create a new portfolio
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Portfolio>> CreatePortfolio(Portfolio portfolio)
        {
            portfolio.LastUpdated = DateTime.UtcNow;
            await mongoDbService.Portfolios.InsertOneAsync(portfolio);
            return CreatedAtAction(nameof(GetPortfolio), new { id = portfolio.Id }, portfolio);
        }

        /// <summary>
        /// Add stock to portfolio
        /// </summary>
        [HttpPost("{id}/stocks")]
        public async Task<IActionResult> AddStockToPortfolio(string id, PortfolioStock portfolioStock)
        {
            var update = Builders<Portfolio>.Update.Push(p => p.Stocks, portfolioStock);
            var result = await mongoDbService.Portfolios.UpdateOneAsync(p => p.Id == id, update);

            if (result.ModifiedCount == 0)
                return NotFound();

            return NoContent();
        }

        /// <summary>
        /// Update portfolio stock quantity
        /// </summary>
        [HttpPut("{id}/stocks/{stockId}")]
        public async Task<IActionResult> UpdateStockQuantity(string id, string stockId, [FromBody] decimal quantity)
        {
            var update = Builders<Portfolio>.Update
                .Set(p => p.Stocks[-1].Quantity, quantity)
                .Set(p => p.LastUpdated, DateTime.UtcNow);

            var result = await mongoDbService.Portfolios.UpdateOneAsync(
                p => p.Id == id && p.Stocks.Any(s => s.StockId == stockId),
                update
            );

            if (result.ModifiedCount == 0)
                return NotFound();

            return NoContent();
        }

        /// <summary>
        /// Remove stock from portfolio
        /// </summary>
        [HttpDelete("{id}/stocks/{stockId}")]
        public async Task<IActionResult> RemoveStockFromPortfolio(string id, string stockId)
        {
            var update = Builders<Portfolio>.Update.PullFilter(
                p => p.Stocks,
                s => s.StockId == stockId
            );

            var result = await mongoDbService.Portfolios.UpdateOneAsync(p => p.Id == id, update);

            if (result.ModifiedCount == 0)
                return NotFound();

            return NoContent();
        }
    }
}