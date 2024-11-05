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
    public class ExchangeRateController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly MongoDbService _mongoDbService;
        private readonly ILogger<ExchangeRateController> _logger;

        public ExchangeRateController(
            IMediator mediator,
            MongoDbService mongoDbService,
            ILogger<ExchangeRateController> logger)
        {
            _mediator = mediator;
            _mongoDbService = mongoDbService;
            _logger = logger;
        }

        /// <summary>
        /// 更新所有Portfolio的匯率
        /// </summary>
        [HttpPut("usd")]
        public async Task<IActionResult> UpdateUsdExchangeRate([FromBody] UpdateExchangeRateRequest request)
        {
            try
            {
                if (request.Rate <= 0)
                {
                    return BadRequest("Exchange rate must be greater than 0");
                }

                // 1. 找出所有有設定匯率的Portfolio
                var portfolios = await _mongoDbService.Portfolios
                    .Find(p => p.ExchangeRate.HasValue)
                    .ToListAsync();

                if (!portfolios.Any())
                {
                    return NotFound("No portfolios with exchange rates found");
                }

                // 2. 發布匯率更新事件
                foreach (var portfolio in portfolios)
                {
                    if (portfolio.ExchangeRate.HasValue)
                    {
                        await _mediator.Publish(new ExchangeRateUpdatedEvent
                        {
                            OldRate = portfolio.ExchangeRate.Value,
                            NewRate = request.Rate,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = request.UpdatedBy ?? "System",
                            Source = request.Source ?? "Manual"
                        });
                    }
                }

                // 3. 批量更新所有Portfolio的匯率
                var update = Builders<Portfolio>.Update
                    .Set(p => p.ExchangeRate, request.Rate)
                    .Set(p => p.ExchangeRateUpdated, DateTime.UtcNow)
                    .Set(p => p.LastUpdated, DateTime.UtcNow);

                var updateResult = await _mongoDbService.Portfolios.UpdateManyAsync(
                    p => p.ExchangeRate.HasValue,
                    update
                );

                _logger.LogInformation(
                    $"Updated exchange rate to {request.Rate}. " +
                    $"Modified {updateResult.ModifiedCount} portfolios"
                );

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
        }

        /// <summary>
        /// 取得目前匯率設定
        /// </summary>
        [HttpGet("usd")]
        public async Task<ActionResult<ExchangeRateResponse>> GetCurrentRate()
        {
            try
            {
                var portfolio = await _mongoDbService.Portfolios
                    .Find(p => p.ExchangeRate.HasValue)
                    .FirstOrDefaultAsync();

                if (portfolio?.ExchangeRate == null)
                {
                    return NotFound("No exchange rate settings found");
                }

                return Ok(new ExchangeRateResponse
                {
                    Rate = portfolio.ExchangeRate.Value,
                    LastUpdated = portfolio.ExchangeRateUpdated
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current exchange rate");
                return StatusCode(500, "An error occurred while getting the exchange rate");
            }
        }
    }
}
