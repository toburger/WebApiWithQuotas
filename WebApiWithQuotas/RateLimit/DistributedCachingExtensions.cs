using Microsoft.Extensions.Caching.Distributed;

namespace WebApiWithQuotas.RateLimit
{
    public static class DistributedCachingExtensions
    {
        public async static Task SetCahceValueAsync<T>(this IDistributedCache distributedCache, string key, T value, CancellationToken token = default(CancellationToken))
        {
            await distributedCache.SetAsync(key, value.ToByteArray(), token);
        }

        public async static Task SetCahceValueAsync<T>(this IDistributedCache distributedCache, string key, int timewindow, T value, CancellationToken token = default(CancellationToken))
        {
            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions();
            options.AbsoluteExpiration = DateTimeOffset.Now.AddSeconds(timewindow);

            await distributedCache.SetAsync(key, value.ToByteArray(), options, token);
        }

        public async static Task<T> GetCacheValueAsync<T>(this IDistributedCache distributedCache, string key, CancellationToken token = default(CancellationToken)) where T : class
        {
            var result = await distributedCache.GetAsync(key, token);
            return result.FromByteArray<T>();
        }
    }
}
