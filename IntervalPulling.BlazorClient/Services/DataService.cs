using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using IntervalPulling.Entities;

namespace IntervalPulling.BlazorClient.Services;

internal interface IDataService
{
    Task<WeatherForecast[]?> GetWeatherForecasts(string id, CancellationToken cancellationToken);
    IAsyncEnumerable<DataService.Result<WeatherForecast[]>> PullingWeatherForecasts(string id, CancellationToken cancellationToken);
}

internal sealed class DataService : IDataService
{
    private readonly HttpClient _httpClient;

    public DataService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri("http://localhost:5140");
    }

    public async Task<WeatherForecast[]?> GetWeatherForecasts(string id, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"WeatherForecast/long-running/{id}", cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<WeatherForecast[]>(stream, cancellationToken: cancellationToken);
        }

        return null;
    }
    
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

    public record Result<T>(Result<T>.States State, T Entity)
    {
        public enum States
        {
            None,
            InProgress,
            Finished,
            Error
        }
    }
}