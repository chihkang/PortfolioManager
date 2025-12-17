using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using PortfolioManager.Services;
using Xunit;

namespace PortfolioManager.Tests;

public sealed class ExchangeRateServiceTests
{
    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(handler(request));
    }

    [Fact]
    public async Task GetExchangeRateAsync_When_Response_Has_DataLastPrice_Parses_And_Rounds()
    {
        const string html = "<div data-last-price=\"31.23456\"></div>";

        var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) }));

        var service = new ExchangeRateService(httpClient, NullLogger<ExchangeRateService>.Instance);

        var rate = await service.GetExchangeRateAsync();

        Assert.Equal(31.2346m, rate);
    }

    [Fact]
    public async Task GetExchangeRateAsync_When_No_Match_Throws()
    {
        const string html = "<html>no rate here</html>";

        var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) }));

        var service = new ExchangeRateService(httpClient, NullLogger<ExchangeRateService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetExchangeRateAsync());
    }
}
