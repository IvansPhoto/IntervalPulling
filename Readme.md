## Disclaimer
In this article, I will explain a solution to a problem I encountered while migrating web services from on-premises to AWS infrastructure and how I solved it.

I can't describe every detail of our project because it's under NDA, but I can paraphrase and create a similar solution on a demo project.

For simplicity and fast development, I used the standard weather forecast templates on .NET 7 for the Web.API and the Blazor WASM.
**IMemoryCache is used to simplify the solution and code brevity.** A solution using IDistributedCache will be similar in terms of business logic, but different in terms of a cache API, infrastructure code and amount of the code.

## Problem
The goal of the project was to move a set of web services from on-premises to AWS and use cloud-native solutions where possible. For this reason, the AWS API Gateway was chosen to protect REST endpoints from the outside world.
During the development phase, we discovered that some REST endpoints require more than 30 seconds to execute, which exceeds the **hardcode limit** for both AWS API Gateway versions and cannot be changed in the near future. Probably the best solution, in this case, is a refactoring to increase performance. Still, the project tried to stick to the **lift and shift** approach and avoid modifying business logic. So, we needed a solution that could work for a while until the project finished migration to a new environment and new owner.

## Solution
I made a demo project to demonstrate the code’s initial state and development process for solving the problem.
The endpoint may look like this:
```csharp
[HttpGet("long-running/{id}")]
public async Task<ActionResult<WeatherForecast[]>> Get(string id)
{
    var result = await _dataService.LongRunningTask(id);
    return Ok(result);
}
```
And a method emulating long-running operations:
```csharp
public async Task<WeatherForecast[]> LongRunningTask(string id)
{
    await Task.Delay(TimeSpan.FromSeconds(50));
    return new WeatherForecast[]
    {
        new()
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(Random.Shared.Next(0, 6))),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        }
    };
}
```
### The first idea
The first idea that comes to mind is to send back a response without waiting for the main code to execute. This approach is commonly used for a case with a message broker and a write operation, but in our case, the operation is reading data, and we need to wait for it.

The most valuable idea that could solve this problem is to perform the calculation in the background and get a result when we need it. To implement this logic, we can use Task.Run() to send execution in the background and a cache to store data.

```csharp
public WeatherForecast[]? GetOrCreateDataFirstIdea(string id)
{
    //Check if there is the entity in the cache
    if (_memoryCache.TryGetValue(id, out WeatherForecast[]? data))
        return data;

    Task.Run(async () =>
    {
        try
        {
            // Run the long running computation on backgroung
            var result = await _dataService.LongRunningTask(id);

            // Store the result in the cache
            _memoryCache.Set(id, result, absoluteExpirationRelativeToNow: TimeSpan.FromMinutes(15));
        }
        catch
        {
            // Log error
            _logger.LogError("_");
        }
    });

    return null;
}
```
The endpoint also needs to be changed in this way:
```csharp
[HttpGet("in-memory-first-idea/{id}")]
public ActionResult<WeatherForecast[]> GetPullingInMemoryFirstIdea(string id)
{
    var result = _inMemoryCacheService.GetOrCreateDataFirstIdea(id);
    return result is null 
        ? NoContent()
        : Ok(result);
}
```
A front-end application must be refactored to use interval pooling to get the result. A Blazor UI variant can be seen at the end of this article.
An important part is a contact based on response codes. In our case, the server returns 204 if the requested entity is not in the cache and 200 if the entity is already computed.

This refactoring can solve the initial problem, but it is not ideal from a performance perspective because each new request creates a new long-running calculation on the same entity until one of them is finished and written to the cache.
### Big improvement
We can improve the solution using the same cache – just store an additional value in the cache with a different key (determines that the entity is in the process of the calculation) and check the value in the cache at the beginning of the method.
```csharp
public CacheServiceResult<WeatherForecast[]> GetOrCreateDataSecondIdea(string id)
{
    if (_memoryCache.TryGetValue(id + InCache, out WeatherForecast[]? data))
        return CacheServiceResult<WeatherForecast[]>.InCache(data!);

    // Store a "InProgress" value to mark the entity in the computation progress.
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
            // Remove the "InProgress" value from the cahce
            _memoryCache.Remove(id + InProgress);
        }
    });

    // Returns the InProgress state, which indicates that the entity is in the process of being calculated.
    return CacheServiceResult<WeatherForecast[]>.InProgress();
}
```

### The final solution
This code still has a place for improvement due to the error handling because each failed request will start a new calculation on the next request from the front-end application.  If the delay between requests is short and the error is not transient, the server's resources will be wasted on unproductive computations.
To avoid this potential waste of resources, we can cache another value with a short lifetime and check for its presence at the beginning of the method.
```csharp
public CacheServiceResult<WeatherForecast[]> GetOrCreateDataFinal(string id)
{
    if (_memoryCache.TryGetValue(id + InCache, out WeatherForecast[]? data))
        return CacheServiceResult<WeatherForecast[]>.InCache(data!);

    if (_memoryCache.TryGetValue(id + InProgress, out _))
        return CacheServiceResult<WeatherForecast[]>.InProgress();

    // Check the value that indicates that the computation of the requested entity terminated with an error. 
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
            // Store a value that that indicates that the computation of the requested entity terminated with an error.
            _memoryCache.Set(id + Error, id, absoluteExpirationRelativeToNow: TimeSpan.FromSeconds(5));
        }
        finally
        {
            _memoryCache.Remove(id + InProgress);
        }
    });

    return CacheServiceResult<WeatherForecast[]>.InProgress();
}
```

CacheServiceResult is a new entity containing an execution result and indicates the execution state.
```csharp
public record CacheServiceResult<T>
{
    private CacheServiceResult(States state, T? entity)
    {
        State = state;
        Entity = entity;
    }

    public static CacheServiceResult<T> InProgress() => new(States.InProgress, default);
    public static CacheServiceResult<T> WithError() => new(States.Error, default);
    public static CacheServiceResult<T> InCache(T entity) => new(States.InCache, entity);

    public enum States
    {
        InProgress,
        InCache,
        Error
    }

    public States State { get; init; }
    public T? Entity { get; init; }
}
```

### The front-end application on Blazor WASM
This is my implementation of a service in Blazor front-end application to request data with interval pooling.
```csharp
public async IAsyncEnumerable<Result<WeatherForecast[]>> PullingWeatherForecasts(string id, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    while (!cancellationToken.IsCancellationRequested)
    {
        var response = await _httpClient.GetAsync($"WeatherForecast/pulling-in-memory-cache/{id}", cancellationToken);
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                var result = await JsonSerializer.DeserializeAsync<WeatherForecast[]>(stream, cancellationToken: cancellationToken);
                yield return new Result<WeatherForecast[]>(Result<WeatherForecast[]>.States.Finished, result!);
                yield break;
            case HttpStatusCode.NoContent:
                yield return new Result<WeatherForecast[]>(Result<WeatherForecast[]>.States.InProgress, Array.Empty<WeatherForecast>());
                break;
            case HttpStatusCode.InternalServerError:
                yield return new Result<WeatherForecast[]>(Result<WeatherForecast[]>.States.Error, Array.Empty<WeatherForecast>());
                break;
           default:
                throw new ArgumentOutOfRangeException();
        }

        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
    }
}
```

This is a part of the Weather forecast page from the standard Blazor WASM template
```csharp
@page "/fetchdata"
***
@switch (_result.State)
{
    case Services.DataService.Result<WeatherForecast[]>.States.None:
        <p><em>Ready to load data.</em></p>
        break;
    case Services.DataService.Result<WeatherForecast[]>.States.InProgress:
        <p><em>Loading...</em></p>
        break;
    case Services.DataService.Result<WeatherForecast[]>.States.Finished:
        <p><em>Finished.</em></p>
        break;
    case Services.DataService.Result<WeatherForecast[]>.States.Error:
        <p><em>Error.</em>
        </p>break;
    default:
        throw new ArgumentOutOfRangeException();
}

@if (_result.State is Services.DataService.Result<WeatherForecast[]>.States.Finished)
{
    <table class="table">
        <thead><tr><th>Date</th><th>Temp. (C)</th><th>Temp. (F)</th><th>Summary</th></tr></thead><tbody>
        @foreach (var forecast in _result.Entity)
        {
            <tr><td>@forecast.Date.ToShortDateString()</td><td>@forecast.TemperatureC</td><td>@forecast.TemperatureF</td><td>@forecast.Summary</td></tr>
        }
        </tbody>
    </table>
}

@code {
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private DataService.Result<WeatherForecast[]> _result = new(Services.DataService.Result<WeatherForecast[]>.States.None, Array.Empty<WeatherForecast>());

    private async Task FetchWeatherForecasts()
    {
        var response = DataService.PullingWeatherForecasts("Type", _cancellationTokenSource.Token);
        await foreach (var result in response)
        {
            _result = result;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
```
## Conclusion
This solution is well-fitted with the "lift and shift" approach, especially if the software is changing the legal owner. It took a couple of days for implementation and saved time for more important parts of our migration.