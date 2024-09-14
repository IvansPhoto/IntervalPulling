using IntervalPulling.Entities;
using IntervalPulling.Rest.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntervalPulling.Rest.Api.Controllers;

[ApiController]
[Route("[controller]")]
internal class WeatherForecastController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly InMemoryCacheService _inMemoryCacheService;
    private readonly DistributedCacheService _distributedCacheService;

    public WeatherForecastController(IDataService dataService, InMemoryCacheService inMemoryCacheService,
        DistributedCacheService distributedCacheService)
    {
        _dataService = dataService;
        _inMemoryCacheService = inMemoryCacheService;
        _distributedCacheService = distributedCacheService;
    }

    [HttpGet("long-running/{id}")]
    public async Task<ActionResult<WeatherForecast[]>> Get(string id)
    {
        var result = await _dataService.LongRunningTask(id);
        return Ok(result);
    }

    [HttpGet("send-message/{id}")]
    public async Task<ActionResult<WeatherForecast[]>> WriteOperation(string id)
    {
        await _dataService.SendMessage(id);
        return Ok();
    }

    [HttpGet("in-memory-first-idea/{id}")]
    public ActionResult<WeatherForecast[]> GetPullingInMemoryFirstIdea(string id)
    {
        var result = _inMemoryCacheService.GetOrCreateDataFirstIdea(id);
        // var result = _inMemoryCacheService.GetOrCreateDataSecondIdea(id);
        return result is null
            ? NoContent()
            : Ok(result);
    }

    [HttpGet("pulling-in-memory-cache/{id}")]
    public ActionResult<WeatherForecast[]> GetPullingInMemory(string id)
    {
        var result = _inMemoryCacheService.GetOrCreateDataFinal(id);
        return Ok(result);
    }


    [HttpGet("pulling-distributed-cache/{id}")]
    public ActionResult<WeatherForecast[]> GetPullingDistributedCache(string id, CancellationToken token)
    {
        var result = _distributedCacheService.GetOrCreateData(id, token);
        return Ok(result);
    }
}