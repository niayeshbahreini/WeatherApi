using Microsoft.EntityFrameworkCore;
using WeatherApi.Data;
using WeatherApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddDbContext<WeatherDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient<IWeatherService, WeatherService>(client =>
    client.Timeout = Timeout.InfiniteTimeSpan);  

builder.Services.AddHostedService<DatabaseInitializer>();  // Init DB

var app = builder.Build();
app.MapControllers();
app.Run();