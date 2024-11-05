using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using PortfolioManager.Events;
using PortfolioManager.Models;
using PortfolioManager.Services;

namespace PortfolioManager.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExchangeRateController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly MongoDbService _mongoDbService;
        private readonly ILogger<ExchangeRateController> _logger;
        private readonly IMemoryCache _cache;
        private const int BatchSize = 100;
        private const string RateCacheKey = "CurrentExchangeRate";
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
        private static readonly SemaphoreSlim UpdateSemaphore = new(1, 1);

        public ExchangeRateController(
            IMediator mediator,
            MongoDbService mongoDbService,
            ILogger<ExchangeRateController> logger,
            IMemoryCache cache)
        {
            _mediator = mediator;
            _mongoDbService = mongoDbService;
            _logger = logger;
            _cache = cache;
        }

        /// <summary>
        /// Updates exchange rates for all portfolios with improved performance
        /// </summary>
        [HttpPut("usd")]
        public async Task<IActionResult> UpdateUsdExchangeRate([FromBody] UpdateExchangeRateRequest request)
        {
            if (request.Rate <= 0)
            {
                return BadRequest("Exchange rate must be greater than 0");
            }

            // Use semaphore to prevent concurrent updates
            if (!await UpdateSemaphore.WaitAsync(TimeSpan.FromSeconds(5)))
            {
                return StatusCode(429, "Another update is in progress. Please try again later.");
            }

            try
            {
                var now = DateTime.UtcNow;

                // 1. Perform direct update first
                var update = Builders<Portfolio>.Update
                    .Set(p => p.ExchangeRate, request.Rate)
                    .Set(p => p.ExchangeRateUpdated, now)
                    .Set(p => p.LastUpdated, now);

                var updateResult = await _mongoDbService.Portfolios
                    .WithWriteConcern(WriteConcern.W1)
                    .UpdateManyAsync(
                        p => p.ExchangeRate.HasValue,
                        update);

                if (updateResult.ModifiedCount == 0)
                {
                    return NotFound("No portfolios with exchange rates found");
                }

                // 2. Get affected portfolios for event publishing
                var portfolioRates = await _mongoDbService.Portfolios
                    .Find(p => p.ExchangeRate.HasValue)
                    .Project(p => new PortfolioRateInfo 
                    { 
                        Id = p.Id, 
                        ExchangeRate = p.ExchangeRate.Value 
                    })
                    .ToListAsync();

                // 3. Publish events in batches
                await PublishEventsInBatches(portfolioRates, request, now);

                // 4. Update cache
                UpdateRateCache(request.Rate, now);

                _logger.LogInformation(
                    "Updated exchange rate to {Rate}. Modified {Count} portfolios",
                    request.Rate,
                    updateResult.ModifiedCount);

                return Ok(new
                {
                    UpdatedRate = request.Rate,
                    ModifiedPortfolios = updateResult.ModifiedCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating exchange rate");
                return StatusCode(500, "An error occurred while updating the exchange rate");
            }
            finally
            {
                UpdateSemaphore.Release();
            }
        }

        private async Task PublishEventsInBatches(
            List<PortfolioRateInfo> portfolioRates,
            UpdateExchangeRateRequest request,
            DateTime timestamp)
        {
            foreach (var batch in portfolioRates.Chunk(BatchSize))
            {
                var batchTasks = batch.Select(p => _mediator.Publish(
                    new ExchangeRateUpdatedEvent
                    {
                        OldRate = p.ExchangeRate,
                        NewRate = request.Rate,
                        UpdatedAt = timestamp,
                        UpdatedBy = request.UpdatedBy ?? "System",
                        Source = request.Source ?? "Manual"
                    }));

                await Task.WhenAll(batchTasks);
            }
        }

        private void UpdateRateCache(decimal rate, DateTime lastUpdated)
        {
            var rateInfo = new ExchangeRateResponse
            {
                Rate = rate,
                LastUpdated = lastUpdated
            };

            _cache.Set(RateCacheKey, rateInfo, CacheDuration);
        }

        /// <summary>
        /// Gets the current exchange rate setting with caching
        /// </summary>
        [HttpGet("usd")]
        public async Task<ActionResult<ExchangeRateResponse>> GetCurrentRate()
        {
            try
            {
                // Try get from cache first
                if (_cache.TryGetValue(RateCacheKey, out ExchangeRateResponse cachedRate))
                {
                    return Ok(cachedRate);
                }

                // If not in cache, get from database
                var rateInfo = await _mongoDbService.Portfolios
                    .Find(p => p.ExchangeRate.HasValue)
                    .Project<ExchangeRateResponse>(Builders<Portfolio>.Projection
                        .Expression(p => new ExchangeRateResponse
                        {
                            Rate = p.ExchangeRate.Value,
                            LastUpdated = p.ExchangeRateUpdated
                        }))
                    .FirstOrDefaultAsync();

                if (rateInfo == null)
                {
                    return NotFound("No exchange rate settings found");
                }

                // Cache the result
                _cache.Set(RateCacheKey, rateInfo, CacheDuration);

                return Ok(rateInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current exchange rate");
                return StatusCode(500, "An error occurred while getting the exchange rate");
            }
        }
    }

    public class PortfolioRateInfo
    {
        public string Id { get; set; }
        public decimal ExchangeRate { get; set; }
    }
}