using Microsoft.EntityFrameworkCore;

namespace WeatherApi.Data;

public class DatabaseInitializer(IServiceProvider sp, ILogger<DatabaseInitializer> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();

                await db.Database.EnsureCreatedAsync(ct);    // Create DB
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Database init failed, retrying");
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
        }
    }
}