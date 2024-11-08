using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace PortfolioManager.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExchangeRateController(HttpClient httpClient) : ControllerBase
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="currencyPair"></param>
    /// <returns></returns>
    /// <exception cref="ScraperException"></exception>
    [HttpGet("{currencyPair}")]
    public async Task<IActionResult> GetExchangeRate(string currencyPair = "USD-TWD")
    {
        try
        {
            string url = $"https://www.google.com/finance/quote/{currencyPair}";
                
            // Set headers
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
            requestMessage.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            HttpResponseMessage response = await httpClient.SendAsync(requestMessage);
            response.EnsureSuccessStatusCode();

            string responseBody = await response.Content.ReadAsStringAsync();

            // Use Regex to find the exchange rate in the response
            var match = Regex.Match(responseBody, @"data-last-price=""([^""]+)""");

            if (!match.Success)
            {
                throw new ScraperException($"Cannot find exchange rate information for {currencyPair}");
            }

            // Parse and round the exchange rate
            double exchangeRate = Math.Round(double.Parse(match.Groups[1].Value), 2);

            return Ok(new { CurrencyPair = currencyPair, ExchangeRate = exchangeRate });
        }
        catch (HttpRequestException e)
        {
            return BadRequest(new { Error = $"Request error while fetching exchange rate: {e.Message}" });
        }
        catch (ScraperException e)
        {
            return NotFound(new { Error = e.Message });
        }
        catch (Exception e)
        {
            return StatusCode(500, new { Error = $"Unexpected error: {e.Message}" });
        }
    }
}