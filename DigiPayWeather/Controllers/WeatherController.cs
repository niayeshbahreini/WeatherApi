using Microsoft.AspNetCore.Mvc;
using WeatherApi.Services;

namespace WeatherApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherController : ControllerBase
{
    private readonly IWeatherService _weatherService;

    public WeatherController(IWeatherService weatherService)
    {
        _weatherService = weatherService;
    }

    [HttpGet]
    public async Task<IActionResult> GetWeather(
        CancellationToken cancellationToken)
    {
        var result = await _weatherService.GetWeatherAsync(
            cancellationToken);

        if (result == null)
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                "Weather service is unavailable.");
        }

        return Content(result, "application/json");
    }
}