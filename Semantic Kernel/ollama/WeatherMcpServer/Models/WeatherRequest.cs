using System.ComponentModel;

namespace WeatherMcpServer.Models;

public sealed class WeatherRequest
{
    [Description("City name, for example Tehran, London, or Berlin always use english name for city")]
    public required string City { get; init; }

    [Description("Number of forecast days (1 to 16)")]
    public int Days { get; init; } = 2;
}
