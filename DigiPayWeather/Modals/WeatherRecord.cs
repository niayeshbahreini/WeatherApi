namespace WeatherApi.Modals;

public class WeatherRecord
{
    public int Id { get; set; }
    public string? Response { get; set; }
    public DateTime? ReceivedAt { get; set; }
}