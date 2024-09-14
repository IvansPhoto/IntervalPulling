using System.Text.Json;
using IntervalPulling.Entities;
using Microsoft.Extensions.Caching.Distributed;

namespace IntervalPulling.Rest.Api.Services;

internal sealed class DistributedCacheService
{
    private const string InProgress = nameof(InProgress);
    private const string Error = nameof(Error);
    private const string InCache = nameof(InCache);
    private readonly ILogger<DistributedCacheService> _logger;
    private readonly IDataService _dataService;
    private readonly IDistributedCache _distributedCache;

    public DistributedCacheService(ILogger<DistributedCacheService> logger, IDistributedCache distributedCache, IDataService dataService)
    {
        _logger = logger;
        _distributedCache = distributedCache;
        _dataService = dataService;
    }

    public async Task<CacheServiceResult<WeatherForecast[]>> GetOrCreateData(string id, CancellationToken token)
    {
        var cachedValue = _distributedCache.GetAsync(id + InCache, token);
        var inProgressValue = _distributedCache.GetAsync(id + InProgress, token);
        var errorValue = _distributedCache.GetAsync(id + Error, token);
        await Task.WhenAll(cachedValue, inProgressValue, errorValue);

        if (cachedValue.Result is not null)
        {
            var entry = JsonSerializer.Deserialize<WeatherForecast[]>(cachedValue.Result);
            return CacheServiceResult<WeatherForecast[]>.InCache(entry!);
        }

        if (inProgressValue.Result is not null)
            return CacheServiceResult<WeatherForecast[]>.InProgress();

        if (errorValue.Result is not null)
            return CacheServiceResult<WeatherForecast[]>.WithError();

        _ = Task.Run(async () =>
        {
            try
            {
                await _distributedCache.SetAsync(id + InProgress, JsonSerializer.SerializeToUtf8Bytes(id), token);

                var result = await _dataService.LongRunningTask(id);

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

        return CacheServiceResult<WeatherForecast[]>.InProgress();
    }
}