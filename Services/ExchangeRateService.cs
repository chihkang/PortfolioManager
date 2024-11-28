using System.Text.RegularExpressions;

namespace PortfolioManager.Services;

public class ExchangeRateService(HttpClient httpClient, ILogger<ExchangeRateService> logger) : IExchangeRateService
{
    public async Task<decimal> GetExchangeRateAsync()
    {
        var url = "https://www.google.com/finance/quote/USD-TWD";
        var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
        requestMessage.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        var response = await httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(responseBody, @"data-last-price=""([^""]+)""");

        if (!match.Success)
        {
            logger.LogWarning("Failed to extract exchange rate");
            throw new InvalidOperationException("Unable to extract exchange rate from response");
        }

        return Math.Round(decimal.Parse(match.Groups[1].Value), 4);
    }
}