namespace WeatherMcpServer.Models;

public sealed class WeatherResponse
{
    public required string City { get; init; }
    public required string Country { get; init; }
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int RequestedDays { get; init; }
    public required IReadOnlyList<WeatherDayForecast> Days { get; init; }
}

public sealed class WeatherDayForecast
{
    public required string Date { get; init; }
    public required string Condition { get; init; }
    public double MinTemperatureC { get; init; }
    public double MaxTemperatureC { get; init; }
    public double PrecipitationMm { get; init; }
    public double MaxWindSpeedKmh { get; init; }
}
