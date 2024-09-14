using IntervalPulling.Entities;

namespace IntervalPulling.Rest.Api.Services;

internal interface IDataService
{
    Task<WeatherForecast[]> LongRunningTask(string id);
    public Task SendMessage(string id);
}

internal sealed class DataService : IDataService
{
    private static readonly string[] Summaries =
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

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

    public Task SendMessage(string id)
    {
       return  Task.CompletedTask;
    }
}