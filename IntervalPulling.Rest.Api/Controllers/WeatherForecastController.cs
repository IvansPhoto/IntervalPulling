using IntervalPulling.Entities;
using IntervalPulling.Rest.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace IntervalPulling.Rest.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class WeatherForecastController : ControllerBase
{
    private readonly IDataService _dataService;
    private readonly InMemoryCacheService _inMemoryCacheService;
    private readonly DistributedCacheService _distributedCacheService;

    public WeatherForecastController(IDataService dataService, InMemoryCacheService inMemoryCacheService, DistributedCacheService distributedCacheService)
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
    
    [HttpGet("pulling/{id}")]
    public ActionResult<WeatherForecast[]> GetPullingInMemory(string id)
    {
        var result = _inMemoryCacheService.GetOrCreateData(id);
        return Ok(result);
    }
    
        
    [HttpGet("pulling/{id}")]
    public ActionResult<WeatherForecast[]> GetPullingDistributedCache(string id)
    {
        var result = _distributedCacheService.GetOrCreateData(id);
        return Ok(result);
    }
}