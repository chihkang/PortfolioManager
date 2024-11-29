using PortfolioManager.Models.Entities;

namespace PortfolioManager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortfolioController(
    MongoDbService mongoDbService,
    ILogger<PortfolioController> logger) : ControllerBase
{
    /// <summary>
    /// Get portfolio by ID using ReadOnlySpan for efficient string handling
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Portfolio>> GetPortfolio([FromRoute] string id)
    {
        ReadOnlySpan<char> idSpan = id.AsSpan();
        if (idSpan.IsEmpty) return BadRequest("Portfolio ID cannot be empty");

        var portfolio = await mongoDbService.Portfolios
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync();

        return portfolio switch
        {
            null => NotFound(),
            _ => Ok(portfolio)
        };
    }

    /// <summary>
    /// Create a new portfolio
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Portfolio>> CreatePortfolio(
        [FromBody] Portfolio portfolio)
    {
        portfolio.LastUpdated = DateTime.UtcNow;

        await mongoDbService.Portfolios.InsertOneAsync(portfolio);
        return CreatedAtAction(
            nameof(GetPortfolio),
            new { id = portfolio.Id },
            portfolio);
    }

    /// <summary>
    /// Update portfolio stock quantity with improved error handling
    /// </summary>
    [HttpPut("{id}/stocks/{stockId}")]
    public async Task<IActionResult> UpdateStockQuantity(
        [FromRoute] string id,
        [FromRoute] string stockId,
        [FromBody] decimal quantity)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentException.ThrowIfNullOrEmpty(stockId);

        try
        {
            var portfolio = await mongoDbService.Portfolios
                .Find(p => p.Id == id)
                .FirstOrDefaultAsync();

            if (portfolio is null)
                return NotFound($"""Portfolio with id {id} not found""");

            var decimalQuantity = new BsonDecimal128(quantity);

            var arrayFilters = new[]
            {
                new BsonDocumentArrayFilterDefinition<BsonDocument>(
                    new BsonDocument("stock.stockId", new ObjectId(stockId)))
            };

            var update = Builders<Portfolio>.Update
                .Set("stocks.$[stock].quantity", decimalQuantity)
                .Set(p => p.LastUpdated, DateTime.UtcNow);

            var result = await mongoDbService.Portfolios.UpdateOneAsync(
                p => p.Id == id,
                update,
                new UpdateOptions { ArrayFilters = arrayFilters });

            return result.ModifiedCount switch
            {
                0 => HandleNoModification(id, stockId, quantity),
                _ => NoContent()
            };
        }
        catch (Exception ex)
        {
            logger.LogError($"""Error updating stock quantity: {ex.Message}""");
            return StatusCode(500, $"""Internal server error: {ex.Message}""");
        }
    }

    private NotFoundObjectResult HandleNoModification(
        string portfolioId,
        string stockId,
        decimal quantity)
    {
        logger.LogWarning(
            $"""No documents modified. Portfolio: {portfolioId}, Stock: {stockId}, Quantity: {quantity}""");
        return NotFound(
            $"""Stock with id {stockId} not found in portfolio {portfolioId}""");
    }

    /// <summary>
    /// Remove stock from portfolio using pattern matching
    /// </summary>
    [HttpDelete("{id}/stocks/{stockId}")]
    public async Task<IActionResult> RemoveStockFromPortfolio(
        [FromRoute] string id,
        [FromRoute] string stockId)
    {
        var update = Builders<Portfolio>.Update
            .PullFilter(p => p.Stocks, s => s.StockId == stockId);

        var result = await mongoDbService.Portfolios
            .UpdateOneAsync(p => p.Id == id, update);

        return result.ModifiedCount switch
        {
            0 => NotFound(),
            _ => NoContent()
        };
    }

    /// <summary>
    /// Get portfolio by username with improved null checking
    /// </summary>
    [HttpGet("user/{username}")]
    public async Task<ActionResult<Portfolio>> GetPortfolioByUsername(
        [FromRoute] string username)
    {
        try
        {
            var user = await mongoDbService.Users
                .Find(u => u.Username == username)
                .FirstOrDefaultAsync();

            if (user is null)
            {
                logger.LogWarning($"""User not found: {username}""");
                return NotFound($"""User '{username}' not found""");
            }

            var portfolio = await mongoDbService.Portfolios
                .Find(p => p.Id == user.PortfolioId)
                .FirstOrDefaultAsync();

            if (portfolio is null)
            {
                logger.LogWarning($"""Portfolio not found for user: {username}""");
                return NotFound($"""Portfolio not found for user '{username}'""");
            }

            return await EnrichPortfolio(portfolio);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"""Error getting portfolio for user: {username}""");
            return StatusCode(500, "An error occurred while retrieving the portfolio");
        }
    }

    private async Task<ActionResult<Portfolio>> EnrichPortfolio(Portfolio portfolio)
    {
        if (!portfolio.Stocks.Any()) return Ok(portfolio);

        var stockIds = portfolio.Stocks
            .Where(s => s.StockId != null)
            .Select(s => s.StockId)
            .ToList();

        var stocks = await mongoDbService.Stocks
            .Find(s => stockIds.Contains(s.Id))
            .ToListAsync();

        var stockDetails = stocks
            .Where(s => s?.Id != null)
            .ToDictionary(s => s.Id);

        var enrichedPortfolio = new EnrichedPortfolioResponse
        {
            Id = portfolio.Id,
            UserId = portfolio.UserId,
            LastUpdated = portfolio.LastUpdated,
            Stocks = portfolio.Stocks
                .Select(ps => new EnrichedPortfolioStock
                {
                    StockId = ps.StockId,
                    Quantity = ps.Quantity,
                    StockDetails = ps.StockId != null && stockDetails.TryGetValue(ps.StockId, out var detail)
                        ? detail
                        : null
                })
                .ToList()
        };

        return Ok(enrichedPortfolio);
    }

    /// <summary>
    /// Add stock to portfolio by stock ID
    /// </summary>
    [HttpPost("{id}/stocks/byId")]
    public async Task<IActionResult> AddStockToPortfolioById(
        [FromRoute] string id,
        [FromBody] AddStockByIdDto stockDto)
    {
        try
        {
            ArgumentException.ThrowIfNullOrEmpty(stockDto.StockId);

            var portfolio = await mongoDbService.Portfolios
                .Find(p => p.Id == id)
                .FirstOrDefaultAsync();

            if (portfolio is null)
                return NotFound($"""Portfolio with id {id} not found""");

            var stock = await mongoDbService.Stocks
                .Find(s => s != null && s.Id == stockDto.StockId)
                .FirstOrDefaultAsync();

            if (stock is null)
                return NotFound($"""Stock with id '{stockDto.StockId}' not found""");

            if (portfolio.Stocks.Any(s => s.StockId == stockDto.StockId))
                return Conflict($"""Stock '{stock.Name}' is already in the portfolio""");

            var portfolioStock = new PortfolioStock
            {
                StockId = stockDto.StockId,
                Quantity = stockDto.Quantity
            };

            var update = Builders<Portfolio>.Update
                .Push(p => p.Stocks, portfolioStock)
                .Set(p => p.LastUpdated, DateTime.UtcNow);

            var result = await mongoDbService.Portfolios
                .UpdateOneAsync(p => p.Id == id, update);

            if (result.ModifiedCount == 0)
            {
                logger.LogError($"""Failed to add stock to portfolio. Portfolio: {id}, Stock: {stockDto.StockId}""");
                return StatusCode(500, "Failed to add stock to portfolio");
            }

            return Ok(new
            {
                Message = $"""Successfully added {stock.Alias} to portfolio""",
                Stock = new
                {
                    stock.Id,
                    stock.Name,
                    stock.Alias,
                    stockDto.Quantity
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"""Error adding stock to portfolio {id}""");
            return StatusCode(500, "An error occurred while adding stock to portfolio");
        }
    }

    /// <summary>
    /// Add stock to portfolio by stock name or alias
    /// </summary>
    [HttpPost("{id}/stocks")]
    public async Task<IActionResult> AddStockToPortfolioByName(
        [FromRoute] string id,
        [FromBody] AddPortfolioStockDto stockDto)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(stockDto.StockNameOrAlias);

            var portfolio = await mongoDbService.Portfolios
                .Find(p => p.Id == id)
                .FirstOrDefaultAsync();

            if (portfolio is null)
                return NotFound($"""Portfolio with id {id} not found""");

            var stockFilter = Builders<Stock>.Filter.Or(
                Builders<Stock>.Filter.Eq(s => s.Name, stockDto.StockNameOrAlias),
                Builders<Stock>.Filter.Eq(s => s.Alias, stockDto.StockNameOrAlias)
            );

            var stock = await mongoDbService.Stocks
                .Find(stockFilter)
                .FirstOrDefaultAsync();

            if (stock is null)
                return NotFound($"""Stock with name or alias '{stockDto.StockNameOrAlias}' not found""");

            if (portfolio.Stocks.Any(s => s.StockId == stock.Id))
                return Conflict($"""Stock '{stockDto.StockNameOrAlias}' is already in the portfolio""");

            var portfolioStock = new PortfolioStock
            {
                StockId = stock.Id,
                Quantity = stockDto.Quantity
            };

            var update = Builders<Portfolio>.Update
                .Push(p => p.Stocks, portfolioStock)
                .Set(p => p.LastUpdated, DateTime.UtcNow);

            var result = await mongoDbService.Portfolios
                .UpdateOneAsync(p => p.Id == id, update);

            if (result.ModifiedCount == 0)
            {
                logger.LogError($"""Failed to add stock to portfolio. Portfolio: {id}, Stock: {stock.Id}""");
                return StatusCode(500, "Failed to add stock to portfolio");
            }

            return Ok(new
            {
                Message = $"""Successfully added {stock.Alias} to portfolio""",
                Stock = new
                {
                    stock.Id,
                    stock.Name,
                    stock.Alias,
                    stockDto.Quantity
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"""Error adding stock to portfolio {id}""");
            return StatusCode(500, "An error occurred while adding stock to portfolio");
        }
    }
}