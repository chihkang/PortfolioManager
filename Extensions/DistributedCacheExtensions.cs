using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

public static class DistributedCacheExtensions
{
    public static async Task<T> GetAsync<T>(this IDistributedCache cache, string key)
    {
        var data = await cache.GetAsync(key);
        if (data == null)
            return default;

        var json = Encoding.UTF8.GetString(data);
        return JsonSerializer.Deserialize<T>(json);
    }

    public static async Task SetAsync<T>(this IDistributedCache cache, string key, T value,
        DistributedCacheEntryOptions options)
    {
        var json = JsonSerializer.Serialize(value);
        var data = Encoding.UTF8.GetBytes(json);
        await cache.SetAsync(key, data, options);
    }
}