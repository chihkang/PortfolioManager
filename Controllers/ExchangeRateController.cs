using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Jobs;

namespace PortfolioManager.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExchangeRateController(
    IExchangeRateService exchangeRateService,
    ILogger<ExchangeRateController> logger)
    : ControllerBase
{
    [HttpGet("{currencyPair}")]
    public async Task<IActionResult> GetExchangeRate(string currencyPair = "USD-TWD")
    {
        try
        {
            var exchangeRate = await exchangeRateService.GetExchangeRateAsync();
            
            return Ok(new ExchangeRateResponse 
            { 
                CurrencyPair = currencyPair, 
                ExchangeRate = exchangeRate 
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching exchange rate for {Pair}", currencyPair);
            return StatusCode(500, new { Error = "無法取得匯率資訊" });
        }
    }
}