using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace IntervalPulling.Rest.Api.Services;

internal sealed class Processor : BackgroundService
{
    private readonly ILogger<Processor> _logger;
    private readonly IOptions<Configuration> _options;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public Processor(ILogger<Processor> logger, IServiceScopeFactory serviceScopeFactory, IOptions<Configuration> options)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            List<Task> tasks = [];
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                for (var i = 0; i < _options.Value.ProcessorNumber; i++)
                {
                    var processor = scope.ServiceProvider.GetRequiredService<DataProcessorUnit>();
                    tasks.Add(processor.Process(stoppingToken));
                }
            }

            await Task.WhenAll(tasks);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An exception occured...");
        }
    }
}

internal sealed class DataProcessorUnit
{
    private readonly ILogger<DataProcessorUnit> _logger;
    private readonly ChannelReader<Data> _reader;
    private readonly IDistributedCache _distributedCache;
    private readonly IDataService _dataService;

    public DataProcessorUnit(ILogger<DataProcessorUnit> logger, IDistributedCache distributedCache, IDataService dataService)
    {
        _reader = Channel.CreateBounded<Data>(1000).Reader;
        _distributedCache = distributedCache;
        _dataService = dataService;
        _logger = logger;
    }

    internal async Task Process(CancellationToken stoppingToken)
    {
        while (await _reader.WaitToReadAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            var id = Data.Empty;

            try
            {
                id = await _reader.ReadAsync(stoppingToken);
                await _distributedCache.SetAsync(id + TaskStatus.InProgress, JsonSerializer.SerializeToUtf8Bytes(id),
                    stoppingToken);
                var result = await _dataService.LongRunningTask(id);
                await _distributedCache.SetAsync(
                    key: id + TaskStatus.InCache,
                    value: JsonSerializer.SerializeToUtf8Bytes(result),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
                    stoppingToken);
            }
            catch(Exception exception)
            {
                _logger.LogError(exception, "An exception occured...");
                await _distributedCache.SetAsync(
                    key: id + TaskStatus.Error,
                    value: JsonSerializer.SerializeToUtf8Bytes(id),
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) },
                    stoppingToken);
            }
            finally
            {
                await _distributedCache.RemoveAsync(id + TaskStatus.InProgress, stoppingToken);
            }
        }
    }
}