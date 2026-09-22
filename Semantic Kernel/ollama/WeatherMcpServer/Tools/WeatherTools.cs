using System.ComponentModel;
using ModelContextProtocol.Server;
using WeatherMcpServer.Models;
using WeatherMcpServer.Services;

namespace WeatherMcpServer.Tools;

[McpServerToolType]
public sealed class WeatherTools(OpenMeteoClient weatherClient)
{
    [McpServerTool(UseStructuredContent = true),
     Description("Returns a weather forecast for a city based on the requested number of days.")]
    public async Task<WeatherResponse> GetCityWeatherForecast(
        WeatherRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.City))
        {
            throw new ArgumentException("City is required.", nameof(request));
        }

        var forecast = await weatherClient.GetForecastAsync(
            request.City.Trim(),
            request.Days,
            cancellationToken);

        return new WeatherResponse
        {
            City = forecast.City,
            Country = forecast.Country,
            Latitude = forecast.Latitude,
            Longitude = forecast.Longitude,
            RequestedDays = request.Days,
            Days = forecast.Days.Select(day => new WeatherDayForecast
            {
                Date = day.Date,
                Condition = day.Condition,
                MinTemperatureC = day.MinTemperatureC,
                MaxTemperatureC = day.MaxTemperatureC,
                PrecipitationMm = day.PrecipitationMm,
                MaxWindSpeedKmh = day.MaxWindSpeedKmh
            }).ToList()
        };
    }
}
