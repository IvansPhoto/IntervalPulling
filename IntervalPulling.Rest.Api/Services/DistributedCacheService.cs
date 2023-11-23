using System.Text.Json;
using IntervalPulling.Entities;
using Microsoft.Extensions.Caching.Distributed;

namespace IntervalPulling.Rest.Api.Services;

public class DistributedCacheService
{
    private const string InProgress = nameof(InProgress);
    private const string Error = nameof(Error);
    private const string InCache = nameof(InCache);
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedCache _distributedCache;

    public DistributedCacheService(ILogger<DistributedCacheService> logger, IServiceProvider serviceProvider, IDistributedCache distributedCache)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _distributedCache = distributedCache;
    }

    public async Task<CacheServiceResult> GetOrCreateData(string id, CancellationToken token = default)
    {
        var cachedValue = _distributedCache.GetAsync(id + InCache, token);
        var inProgressValue = _distributedCache.GetAsync(id + InProgress, token);
        var errorValue = _distributedCache.GetAsync(id + Error, token);
        await Task.WhenAll(cachedValue, inProgressValue, errorValue);

        if (cachedValue.Result is not null)
        {
            var entry = JsonSerializer.Deserialize<WeatherForecast[]>(cachedValue.Result);
            return CacheServiceResult.InCache(entry!);
        }

        if (inProgressValue.Result is not null)
            return CacheServiceResult.InProgress();

        if (errorValue.Result is not null)
            return CacheServiceResult.WithError();

        _ = Task.Run(async () =>
        {
            try
            {
                await _distributedCache.SetAsync(id + InProgress, JsonSerializer.SerializeToUtf8Bytes(id), token);

                using var scope = _serviceProvider.CreateScope();
                var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
                var result = await dataService.LongRunningTask(id);

                await _distributedCache.SetAsync(
                    key: id + InCache,
                    value: JsonSerializer.SerializeToUtf8Bytes(result),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
                    token);
            }
            catch
            {
                _logger.LogError("_");
                await _distributedCache.SetAsync(
                    key: id + Error,
                    value: JsonSerializer.SerializeToUtf8Bytes(id),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
                    token);
            }
            finally
            {
                await _distributedCache.RemoveAsync(id + InProgress, token);
            }
        }, token);

        return CacheServiceResult.InProgress();
    }
}