using IntervalPulling.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace IntervalPulling.Rest.Api.Services;

public sealed class InMemoryCacheService
{
    private const string InProgress = nameof(InProgress);
    private const string Error = nameof(Error);
    private const string InCache = nameof(InCache);
    private readonly ILogger<InMemoryCacheService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMemoryCache _memoryCache;
    
    public InMemoryCacheService(ILogger<InMemoryCacheService> logger, IServiceProvider serviceProvider, IMemoryCache memoryCache)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _memoryCache = memoryCache;
    }

    public CacheServiceResult GetOrCreateData(string id)
    {
        if (_memoryCache.TryGetValue(id + InCache, out WeatherForecast[]? data))
            return CacheServiceResult.InCache(data!);
        
        if (_memoryCache.TryGetValue(id + InProgress,out _))
            return CacheServiceResult.InProgress();
        
        if (_memoryCache.TryGetValue(id + Error,out _))
            return CacheServiceResult.WithError();
        
        Task.Run(async () =>
        {
            try
            {
                _memoryCache.Set(id + InProgress, id);

                using var scope = _serviceProvider.CreateScope();
                var dataService = scope.ServiceProvider.GetRequiredService<IDataService>();
                var result = await dataService.LongRunningTask(id);

                _memoryCache.Set(id + InCache, result, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(15));
            }
            catch
            {
                _logger.LogError("_");
                _memoryCache.Set(id + Error, id, absoluteExpirationRelativeToNow: TimeSpan.FromSeconds(15));
            }
            finally
            {
                _memoryCache.Remove(id + InProgress);
            }
        });

        return CacheServiceResult.InProgress();
    }
}