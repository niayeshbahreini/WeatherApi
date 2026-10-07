namespace WeatherApi.Services;

public interface IWeatherService
{
    Task<string?> GetWeatherAsync(CancellationToken cancellationToken);
}