using System.Text.Json;
using System.Threading.Channels;
using IntervalPulling.Entities;
using Microsoft.Extensions.Caching.Distributed;

namespace IntervalPulling.Rest.Api.Services;

internal sealed class DistributedCacheServiceChannels
{
    private readonly ILogger<DistributedCacheServiceChannels> _logger;
    private readonly IDistributedCache _distributedCache;
    private readonly ChannelWriter<Data> _writer;

    public DistributedCacheServiceChannels(ILogger<DistributedCacheServiceChannels> logger,
        IDistributedCache distributedCache)
    {
        _logger = logger;
        _distributedCache = distributedCache;
        _writer = Channel.CreateBounded<Data>(1000).Writer;
    }

    public async Task<CacheServiceResult<WeatherForecast[]>> GetOrCreateData(string id, CancellationToken token)
    {
        var cachedValue = _distributedCache.GetAsync(id + TaskStatus.InCache, token);
        var inProgressValue = _distributedCache.GetAsync(id + TaskStatus.InProgress, token);
        var errorValue = _distributedCache.GetAsync(id + TaskStatus.Error, token);
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

        if (_writer.TryWrite(new Data { Id = id }))
            return CacheServiceResult<WeatherForecast[]>.InProgress();

        _logger.LogError("Channel is full");
        return CacheServiceResult<WeatherForecast[]>.WithError();
    }
}

internal enum TaskStatus
{
    InProgress,
    Error,
    InCache
}

internal readonly record struct Data
{
    public required string Id { get; init; }
    public static implicit operator string(Data data) => data.Id;

    public static Data Empty { get; } = new() { Id = string.Empty };
};