# Weather API

A small .NET Web API that gets weather data from an external service, stores the response in SQL Server, and returns the same response to the caller.

The application is designed for a system that may need to run for a long time without manual maintenance. Because of that, the implementation keeps the number of moving parts small and handles temporary failures where possible.

## How it works

The main endpoint is:

`GET /api/weather`

When the endpoint is called:

1. The application requests fresh weather data from the external API.
2. It retries the request up to 3 times if the request fails.
3. If a response is received, it is stored in the database and returned to the caller.
4. If all attempts fail, the last stored response is returned.
5. If there is no stored response, the endpoint returns `null`.

The endpoint is designed to respond within 5 seconds.

## Requirements and implementation

| Requirement | Implementation |
|---|---|
| .NET REST API | `GET /api/weather` |
| Regular controller | Uses `[ApiController]`, not Minimal API |
| SQL Server | Entity Framework Core |
| Return the response without changing it | The response is stored as raw JSON text |
| Network failures | Up to 3 attempts with time limits |
| Failed external API | Uses the last saved response |
| No previous response | Returns `null` with HTTP 200 |
| Repeated requests | Keeps only one database row |
| Long-term operation | Limited dependencies and no unnecessary background work |

## Time limits

There are separate limits for the external request and database operation:

| Operation | Limit |
|---|---:|
| One external API attempt | 1.5 seconds |
| External API attempts in total | 3.5 seconds |
| One database operation | 0.7 seconds |

The normal worst-case path is therefore:

`3.5 + 0.7 = 4.2 seconds`

This leaves some margin under the 5-second endpoint limit.

The total timeout is also taken into account when retrying. For example, if the first attempts take longer than expected, the last attempt only gets the remaining time.

## Database design

The `WeatherRecords` table contains a single row.

Instead of inserting a new record every time the endpoint is called, the application updates the existing row with `Id = 1`.

There are a few reasons for this:

- The database does not keep growing when the endpoint is called frequently.
- Reading the latest response is straightforward.
- There is no need to depend on timestamps to find the latest record.
- The database stays small over a long period of time.

`Id = 1` is used as a fixed key. Identity generation is disabled for this table, so the application always works with the same row.

On the first successful save, the row may not exist yet. In that case, the application creates it. If the row is deleted later, the next successful save creates it again.

## Project structure

```text
DigiPayWeather/
├── Controllers/
│   └── WeatherController.cs
├── Data/
│   ├── WeatherDbContext.cs
│   └── DatabaseInitializer.cs
├── Models/
│   └── WeatherRecord.cs
├── Services/
│   └── WeatherService.cs
├── appsettings.json
├── Program.cs
└── DigiPayWeather.csproj
```

### Main files

**WeatherController.cs**

Handles the HTTP request and calls `WeatherService`.

`Content(...)` is used when returning the stored response because `Ok(null)` would result in an empty 204 response instead of a response containing the text `null`.

**WeatherService.cs**

Contains the main application logic:

- `FetchAsync` calls the external weather API, handles retries and time limits, and checks that the response is valid JSON.
- `SaveAsync` updates the single database row or creates it if it does not exist.
- `ReadLastAsync` reads the previously saved response when the external API cannot be reached.

A database failure does not prevent a fresh response from being returned if the external API request succeeded.

**DatabaseInitializer.cs**

Initializes the database when the application starts.

If SQL Server is not available yet, it waits 10 seconds and tries again. The application itself does not block while waiting for the database.

**WeatherDbContext.cs / WeatherRecord.cs**

Define the database model. The stored data consists of:

- `Id`
- `Response`
- `ReceivedAt`

**Program.cs**

Registers the required services, configures the application, and starts the API.

The HTTP client does not use a fixed built-in timeout because the service controls the request timeout itself.

## Configuration

The main settings are in `appsettings.json`:

| Setting | Description |
|---|---|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string |
| `WeatherApi:Url` | External weather API URL |
| `Kestrel:Endpoints:Http:Url` | Address used by the application |
| `Logging` | Console logging configuration |

The development configuration uses LocalDB. For deployment, the SQL Server connection string should be supplied through an environment variable, for example:

```text
ConnectionStrings__DefaultConnection="Server=...;Database=WeatherDb;..."
```

## Long-running deployment

The application is kept intentionally simple because it is expected to run with minimal maintenance.

A few points that are important for deployment:

- The application can be published as self-contained, so the required .NET runtime is included.
- `InvariantGlobalization` avoids depending on ICU being installed on the target Linux machine.
- The application can be managed by `systemd` with `Restart=always`.
- Database initialization runs independently from the API startup.
- A database outage does not stop the API from returning fresh data.
- Logging is kept on the console instead of writing long-term log files to disk.
- The application does not depend on the system clock for selecting the stored response.
- The project uses only a small number of external packages.

## Run locally

```bash
dotnet run
```

Then open:

```text
https://localhost:7079/api/weather
```

The actual port may be different depending on the local configuration. Check the application output after starting it.

## Publish

For a Linux x64 deployment:

```bash
dotnet publish -c Release -r linux-x64 --self-contained true -o ./out
```

The published application can then be run as a `systemd` service with `Restart=always`.

## Testing

The main scenarios to check are:

1. **Normal request**

   Call the endpoint several times and verify that the table still contains one row and that `ReceivedAt` is updated.

2. **External API unavailable**

   Disable the internet connection and call the endpoint. The last saved response should be returned within the 5-second limit.

3. **No internet and no saved response**

   Clear the stored response:

   ```sql
   UPDATE WeatherRecords
   SET Response = NULL, ReceivedAt = NULL
   WHERE Id = 1
   ```

   Then call the endpoint without internet access. The response should be `null` with HTTP 200.

4. **Database unavailable**

   Use an invalid database connection string. A successful external API request should still be returned to the client, even though it cannot be saved.

## Known limitations

There are a couple of edge cases that are not handled by the current implementation.

### Changes inside otherwise valid JSON

If the network corrupts a value but the resulting JSON is still valid, the application cannot detect it. There is no checksum or reference value in the task that could be used to verify the contents.

### Concurrent requests

Two requests can reach the save operation at almost the same time. In that situation, an older response could potentially overwrite a newer one.

The stored data is still valid, but it may not be the most recent response. Handling this case with additional concurrency logic would make the implementation more complicated, so it is intentionally kept out of this version.

## Kept simple on purpose

This project does not use Swagger, EF Core migrations, a repository layer, a service interface, AutoMapper, DTOs, or Polly.

The retry logic is handled with a simple loop.

For this particular task, adding more layers or packages would increase the amount of code without solving a requirement that the application actually has.
