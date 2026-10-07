using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WeatherApi.Data;

namespace WeatherApi.Services;

public class WeatherService(
    HttpClient httpClient,
    WeatherDbContext dbContext,
    IConfiguration configuration,
    ILogger<WeatherService> logger) : IWeatherService
{
    private static readonly TimeSpan FetchBudget = TimeSpan.FromSeconds(3.5);
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan DbTimeout = TimeSpan.FromSeconds(0.7);
    private const int MaxAttempts = 3;

    public async Task<string?> GetWeatherAsync(CancellationToken _)
    {
        var fresh = await FetchAsync();
        if (fresh is not null)
        {
            await SaveAsync(fresh);
            return fresh;
        }
        return await ReadLastAsync();
    }

    private async Task<string?> FetchAsync()
    {
        using var budget = new CancellationTokenSource(FetchBudget);

        for (var i = 0; i < MaxAttempts && !budget.IsCancellationRequested; i++)
        {
            try
            {
                using var attempt = CancellationTokenSource.CreateLinkedTokenSource(budget.Token);
                attempt.CancelAfter(AttemptTimeout);

                var json = await httpClient.GetStringAsync(
                    configuration["WeatherApi:Url"], attempt.Token);

                JsonDocument.Parse(json).Dispose();      // Validate JSON 
                return json;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Weather fetch attempt {Attempt} failed", i + 1);
            }
        }
        return null;
    }

    private async Task SaveAsync(string json)
    {
        try
        {
            using var cts = new CancellationTokenSource(DbTimeout);
            await dbContext.WeatherRecords
                .Where(x => x.Id == 1)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Response, json)
                    .SetProperty(x => x.ReceivedAt, DateTime.UtcNow), cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Saving weather failed");   
        }
    }

    private async Task<string?> ReadLastAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(DbTimeout);
            return await dbContext.WeatherRecords
                .Where(x => x.Id == 1)
                .Select(x => x.Response)
                .FirstOrDefaultAsync(cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Reading last weather failed");
            return null;
        }
    }
}