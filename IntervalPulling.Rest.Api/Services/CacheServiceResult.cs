using IntervalPulling.Entities;

namespace IntervalPulling.Rest.Api.Services;

public record CacheServiceResult
{
    private CacheServiceResult(States state, WeatherForecast[] weatherForecasts)
    {
        State = state;
        WeatherForecasts = weatherForecasts;
    }

    public static CacheServiceResult InProgress() => new(States.InProgress, Array.Empty<WeatherForecast>());
    public static CacheServiceResult WithError() => new(States.Error, Array.Empty<WeatherForecast>());
    public static CacheServiceResult InCache(WeatherForecast[] forecasts) => new(States.InCache, forecasts);

    public enum States
    {
        InProgress,
        InCache,
        Error
    }

    public States State { get; init; }
    public WeatherForecast[] WeatherForecasts { get; init; }
}