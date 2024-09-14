using IntervalPulling.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace IntervalPulling.Rest.Api.Services;

internal sealed class InMemoryCacheService
{
    private const string InProgress = nameof(InProgress);
    private const string Error = nameof(Error);
    private const string InCache = nameof(InCache);
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly IDataService _dataService;
    private readonly IMemoryCache _memoryCache;

    public InMemoryCacheService(ILogger<InMemoryCacheService> logger, IDataService dataService,
        IMemoryCache memoryCache)
    {
        _logger = logger;
        _dataService = dataService;
        _memoryCache = memoryCache;
    }

    public CacheServiceResult<WeatherForecast[]> GetOrCreateDataFinal(string id)
    {
        if (_memoryCache.TryGetValue(id + InCache, out WeatherForecast[]? data))
            return CacheServiceResult<WeatherForecast[]>.InCache(data!);

        if (_memoryCache.TryGetValue(id + InProgress, out _))
            return CacheServiceResult<WeatherForecast[]>.InProgress();

        if (_memoryCache.TryGetValue(id + Error, out _))
            return CacheServiceResult<WeatherForecast[]>.WithError();

        Task.Run(async () =>
        {
            try
            {
                _memoryCache.Set(id + InProgress, id);

                var result = await _dataService.LongRunningTask(id);

                _memoryCache.Set(id + InCache, result, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(15));
            }
            catch
            {
                _logger.LogError("_");
                _memoryCache.Set(id + Error, id, absoluteExpirationRelativeToNow: TimeSpan.FromSeconds(5));
            }
            finally
            {
                _memoryCache.Remove(id + InProgress);
            }
        });

        return CacheServiceResult<WeatherForecast[]>.InProgress();
    }

    public WeatherForecast[]? GetOrCreateDataFirstIdea(string id)
    {
        if (_memoryCache.TryGetValue(id, out WeatherForecast[]? data))
            return data;

        Task.Run(async () =>
        {
            try
            {
                var result = await _dataService.LongRunningTask(id);

                _memoryCache.Set(id, result, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(15));
            }
            catch
            {
                _logger.LogError("_");
            }
        });

        return null;
    }

    public CacheServiceResult<WeatherForecast[]> GetOrCreateDataSecondIdea(string id)
    {
        if (_memoryCache.TryGetValue(id + InCache, out WeatherForecast[]? data))
            return CacheServiceResult<WeatherForecast[]>.InCache(data!);

        if (_memoryCache.TryGetValue(id + InProgress, out _))
            return CacheServiceResult<WeatherForecast[]>.InProgress();

        Task.Run(async () =>
        {
            try
            {
                _memoryCache.Set(id + InProgress, id);
                var result = await _dataService.LongRunningTask(id);
                _memoryCache.Set(id + InCache, result, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(15));
            }
            catch
            {
                _logger.LogError("_");
            }
            finally
            {
                _memoryCache.Remove(id + InProgress);
            }
        });

        return CacheServiceResult<WeatherForecast[]>.InProgress();
    }
}