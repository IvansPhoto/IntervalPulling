using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IntervalPulling.Entities;

namespace IntervalPulling.BlazorClient.Services;

public interface IDataService
{
    Task<WeatherForecast[]?> GetWeatherForecasts(string id, CancellationToken cancellationToken);
    IAsyncEnumerable<DataService.Result> PullingWeatherForecasts(string id, CancellationToken cancellationToken);
}

public sealed class DataService : IDataService
{
    private readonly ILogger<DataService> _logger;
    private readonly HttpClient _httpClient;

    public DataService(ILogger<DataService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5140");
    }

    public async Task<WeatherForecast[]?> GetWeatherForecasts(string id, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"WeatherForecast/long-running/{id}", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var result = await JsonSerializer.DeserializeAsync<WeatherForecast[]>(stream, cancellationToken: cancellationToken);
            return result;
        }

        return null;
    }
    
    public async IAsyncEnumerable<Result> PullingWeatherForecasts(string id, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (true)
        {
            var response = await _httpClient.GetAsync($"WeatherForecast/long-running/{id}", cancellationToken);
            switch (response.StatusCode)
            {
                case HttpStatusCode.OK:
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    var result = await JsonSerializer.DeserializeAsync<WeatherForecast[]>(stream, cancellationToken: cancellationToken);
                    yield return new Result(Result.States.Finished, result!);
                    yield break;
                case HttpStatusCode.Accepted:
                    yield return new Result(Result.States.Accepted, Array.Empty<WeatherForecast>());
                    break;
                case HttpStatusCode.NoContent:
                    yield return new Result(Result.States.InProgress, Array.Empty<WeatherForecast>());
                    break;
                case HttpStatusCode.InternalServerError:
                    yield return new Result(Result.States.Error, Array.Empty<WeatherForecast>());
                    break;
               default:
                    throw new ArgumentOutOfRangeException();
            }

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    public record Result(Result.States State, WeatherForecast[] WeatherForecasts)
    {
        public enum States
        {
            None,
            Accepted,
            InProgress,
            Finished,
            Error
        }
    }
}