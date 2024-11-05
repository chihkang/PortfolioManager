using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using PortfolioManager.Models;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PortfolioController(MongoDbService mongoDbService, ILogger<PortfolioController> logger) : ControllerBase
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
        
        /// <summary>
        /// Get portfolio by username
        /// </summary>
        [HttpGet("user/{username}")]
        public async Task<ActionResult<Portfolio>> GetPortfolioByUsername(string username)
        {
            try
            {
                // 1. 先找到用戶
                var user = await mongoDbService.Users
                    .Find(u => u.Username == username)
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    logger.LogWarning($"User not found: {username}");
                    return NotFound($"User '{username}' not found");
                }

                // 2. 用 portfolioId 查找 portfolio
                var portfolio = await mongoDbService.Portfolios
                    .Find(p => p.Id == user.PortfolioId)
                    .FirstOrDefaultAsync();

                if (portfolio == null)
                {
                    logger.LogWarning($"Portfolio not found for user: {username}");
                    return NotFound($"Portfolio not found for user '{username}'");
                }

                // 3. 加載 portfolio 中股票的詳細資訊
                if (portfolio.Stocks?.Any() == true)
                {
                    var stockIds = portfolio.Stocks.Select(s => s.StockId).ToList();
                    var stocks = await mongoDbService.Stocks
                        .Find(s => stockIds.Contains(s.Id))
                        .ToListAsync();

                    var stockDetails = stocks.ToDictionary(s => s.Id);

                    // 豐富回應資訊
                    var enrichedPortfolio = new EnrichedPortfolioResponse
                    {
                        Id = portfolio.Id,
                        TotalValue = portfolio.TotalValue,
                        ExchangeRate = portfolio.ExchangeRate,
                        ExchangeRateUpdated = portfolio.ExchangeRateUpdated,
                        LastUpdated = portfolio.LastUpdated,
                        Stocks = portfolio.Stocks.Select(ps => new EnrichedPortfolioStock
                        {
                            StockId = ps.StockId,
                            Quantity = ps.Quantity,
                            PercentageOfTotal = ps.PercentageOfTotal,
                            StockDetails = stockDetails.GetValueOrDefault(ps.StockId)
                        }).ToList()
                    };

                    return Ok(enrichedPortfolio);
                }

                return Ok(portfolio);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error getting portfolio for user: {username}");
                return StatusCode(500, "An error occurred while retrieving the portfolio");
            }
        }
        
    }
    public class EnrichedPortfolioResponse
    {
        public string Id { get; set; }
        public decimal TotalValue { get; set; }
        public decimal? ExchangeRate { get; set; }
        public DateTime ExchangeRateUpdated { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<EnrichedPortfolioStock> Stocks { get; set; }
    }

    public class EnrichedPortfolioStock
    {
        public string StockId { get; set; }
        public decimal Quantity { get; set; }
        public decimal PercentageOfTotal { get; set; }
        public Stock StockDetails { get; set; }
    }
}