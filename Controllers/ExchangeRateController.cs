using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Jobs;

namespace PortfolioManager.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExchangeRateController : ControllerBase
{
    private readonly IExchangeRateService _exchangeRateService;
    private readonly ILogger<ExchangeRateController> _logger;

    public ExchangeRateController(
        IExchangeRateService exchangeRateService,
        ILogger<ExchangeRateController> logger)
    {
        _exchangeRateService = exchangeRateService;
        _logger = logger;
    }

    [HttpGet("{currencyPair}")]
    public async Task<IActionResult> GetExchangeRate(string currencyPair = "USD-TWD")
    {
        try
        {
            var exchangeRate = await _exchangeRateService.GetExchangeRateAsync();
            
            return Ok(new ExchangeRateResponse 
            { 
                CurrencyPair = currencyPair, 
                ExchangeRate = exchangeRate 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching exchange rate for {Pair}", currencyPair);
            return StatusCode(500, new { Error = "無法取得匯率資訊" });
        }
    }
}