using Microsoft.Extensions.Caching.Distributed;
using PortfolioManager.Extensions;
using Xunit;

namespace PortfolioManager.Tests;

public sealed class DistributedCacheExtensionsTests
{
    private sealed class FakeDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _store = new(StringComparer.Ordinal);

        public byte[]? Get(string key) => _store.TryGetValue(key, out var value) ? value : null;

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromResult(Get(key));

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            _store[key] = value;

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed record SamplePayload(int Id, string Name);

    [Fact]
    public async Task SetAsync_Then_GetAsync_RoundTrips_Value()
    {
        var cache = new FakeDistributedCache();
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) };
        var payload = new SamplePayload(42, "hello");

        await cache.SetAsync("k", payload, options);
        var readBack = await cache.GetAsync<SamplePayload>("k");

        Assert.Equal(payload, readBack);
    }

    [Fact]
    public async Task GetAsync_When_Missing_Returns_Null()
    {
        var cache = new FakeDistributedCache();
        var readBack = await cache.GetAsync<SamplePayload>("missing");
        Assert.Null(readBack);
    }
}
