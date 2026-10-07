using Microsoft.EntityFrameworkCore;
using WeatherApi.Modals;

namespace WeatherApi.Data;

public class WeatherDbContext(DbContextOptions<WeatherDbContext> options) : DbContext(options)
{
    public DbSet<WeatherRecord> WeatherRecords => Set<WeatherRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<WeatherRecord>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();

            e.HasData(new WeatherRecord { Id = 1 });    // Seed record
        });
    }
}