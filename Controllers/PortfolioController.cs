using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
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
        /// Update portfolio stock quantity
        /// </summary>
        [HttpPut("{id}/stocks/{stockId}")]
        public async Task<IActionResult> UpdateStockQuantity(string id, string stockId, [FromBody] decimal quantity)
        {
            try 
            {
                // 確認文檔是否存在
                var portfolio = await mongoDbService.Portfolios
                    .Find(p => p.Id == id)
                    .FirstOrDefaultAsync();
            
                if (portfolio == null)
                {
                    return NotFound($"Portfolio with id {id} not found");
                }

                // 使用 BsonDecimal128 來確保正確的數字類型
                var decimalQuantity = new BsonDecimal128(quantity);
        
                var update = Builders<Portfolio>.Update
                    .Set("stocks.$[stock].quantity", decimalQuantity)
                    .Set(p => p.LastUpdated, DateTime.UtcNow);

                var arrayFilters = new[]
                {
                    new BsonDocumentArrayFilterDefinition<BsonDocument>(
                        new BsonDocument("stock.stockId", new ObjectId(stockId))  // 使用 ObjectId
                    )
                };

                var updateOptions = new UpdateOptions 
                { 
                    ArrayFilters = arrayFilters
                };

                var result = await mongoDbService.Portfolios.UpdateOneAsync(
                    p => p.Id == id,
                    update,
                    updateOptions
                );

                if (result.ModifiedCount == 0)
                {
                    // 如果沒有更新任何文檔，記錄更多資訊
                    logger.LogWarning($"No documents modified. Portfolio: {id}, Stock: {stockId}, Quantity: {quantity}");
                    return NotFound($"Stock with id {stockId} not found in portfolio {id}");
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                logger.LogError($"Error updating stock quantity: {ex.Message}");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
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
                        userId = portfolio.UserId,
                        LastUpdated = portfolio.LastUpdated,
                        Stocks = portfolio.Stocks.Select(ps => new EnrichedPortfolioStock
                        {
                            StockId = ps.StockId,
                            Quantity = ps.Quantity,
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
        /// <summary>
        /// Add stock to portfolio by stock name or alias
        /// </summary>
        [HttpPost("{id}/stocks")]
        public async Task<IActionResult> AddStockToPortfolio(string id, AddPortfolioStockDto stockDto)
        {
            try
            {
                // 1. 驗證輸入
                if (string.IsNullOrWhiteSpace(stockDto.StockNameOrAlias))
                {
                    return BadRequest("Stock name or alias is required");
                }

                // 2. 檢查投資組合是否存在
                var portfolio = await mongoDbService.Portfolios
                    .Find(p => p.Id == id)
                    .FirstOrDefaultAsync();

                if (portfolio == null)
                {
                    return NotFound($"Portfolio with id {id} not found");
                }

                // 3. 查找股票
                var stockFilter = Builders<Stock>.Filter.Or(
                    Builders<Stock>.Filter.Eq(s => s.Name, stockDto.StockNameOrAlias),
                    Builders<Stock>.Filter.Eq(s => s.Alias, stockDto.StockNameOrAlias)
                );

                var stock = await mongoDbService.Stocks
                    .Find(stockFilter)
                    .FirstOrDefaultAsync();

                if (stock == null)
                {
                    return NotFound($"Stock with name or alias '{stockDto.StockNameOrAlias}' not found");
                }

                // 4. 檢查股票是否已經在投資組合中
                if (portfolio.Stocks != null && 
                    portfolio.Stocks.Any(s => s.StockId == stock.Id))
                {
                    return Conflict($"Stock '{stockDto.StockNameOrAlias}' is already in the portfolio");
                }

                // 5. 創建新的 PortfolioStock
                var portfolioStock = new PortfolioStock
                {
                    StockId = stock.Id,
                    Quantity = stockDto.Quantity
                };

                // 6. 更新投資組合
                var update = Builders<Portfolio>.Update
                    .Push(p => p.Stocks, portfolioStock)
                    .Set(p => p.LastUpdated, DateTime.UtcNow);

                var result = await mongoDbService.Portfolios
                    .UpdateOneAsync(p => p.Id == id, update);

                if (result.ModifiedCount == 0)
                {
                    logger.LogError($"Failed to add stock to portfolio. Portfolio: {id}, Stock: {stock.Id}");
                    return StatusCode(500, "Failed to add stock to portfolio");
                }

                // 7. 返回成功響應
                return Ok(new 
                {
                    Message = $"Successfully added {stock.Alias ?? stock.Name} to portfolio",
                    Stock = new
                    {
                        Id = stock.Id,
                        Name = stock.Name,
                        Alias = stock.Alias,
                        Quantity = stockDto.Quantity
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error adding stock to portfolio {id}");
                return StatusCode(500, "An error occurred while adding stock to portfolio");
            }
        }
        
    }
    public class EnrichedPortfolioResponse
    {
        public string Id { get; set; }
        public DateTime LastUpdated { get; set; }
        public List<EnrichedPortfolioStock> Stocks { get; set; }
        public string userId { get; set; }
    }

    public class EnrichedPortfolioStock
    {
        public string StockId { get; set; }
        public decimal Quantity { get; set; }
        public Stock StockDetails { get; set; }
    }
}