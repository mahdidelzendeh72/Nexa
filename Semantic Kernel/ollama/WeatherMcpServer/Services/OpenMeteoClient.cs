using System.Globalization;
using System.Text.Json.Serialization;

namespace WeatherMcpServer.Services;

public sealed class OpenMeteoClient
{
    private static readonly HttpClient Http = new()
    {
        BaseAddress = new Uri("https://api.open-meteo.com/")
    };

    private static readonly HttpClient GeocodingHttp = new()
    {
        BaseAddress = new Uri("https://geocoding-api.open-meteo.com/")
    };

    public async Task<WeatherForecastResult> GetForecastAsync(string city, int days, CancellationToken cancellationToken)
    {
        if (days is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(days), "Days must be between 1 and 16.");
        }

        var location = await ResolveCityAsync(city, cancellationToken)
            ?? throw new InvalidOperationException($"City '{city}' was not found.");

        var forecastUri =
            $"v1/forecast?latitude={location.Latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={location.Longitude.ToString(CultureInfo.InvariantCulture)}" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_sum,wind_speed_10m_max" +
            $"&forecast_days={days}&timezone=auto";

        var forecast = await Http.GetFromJsonAsync<ForecastResponse>(forecastUri, cancellationToken)
            ?? throw new InvalidOperationException("Weather service returned an empty response.");

        if (forecast.Daily is null || forecast.Daily.Time.Length == 0)
        {
            throw new InvalidOperationException("No forecast data is available for this city.");
        }

        var forecastDays = new List<DailyWeather>();
        for (var i = 0; i < forecast.Daily.Time.Length; i++)
        {
            forecastDays.Add(new DailyWeather(
                Date: forecast.Daily.Time[i],
                MinTemperatureC: forecast.Daily.TemperatureMin[i],
                MaxTemperatureC: forecast.Daily.TemperatureMax[i],
                PrecipitationMm: forecast.Daily.PrecipitationSum[i],
                MaxWindSpeedKmh: forecast.Daily.WindSpeedMax[i],
                Condition: DescribeWeatherCode(forecast.Daily.WeatherCode[i])));
        }

        return new WeatherForecastResult(
            City: location.Name,
            Country: location.Country,
            Latitude: location.Latitude,
            Longitude: location.Longitude,
            Days: forecastDays);
    }

    private static async Task<CityLocation?> ResolveCityAsync(string city, CancellationToken cancellationToken)
    {
        var uri = $"v1/search?name={Uri.EscapeDataString(city)}&count=1&language=en&format=json";
        var response = await GeocodingHttp.GetFromJsonAsync<GeocodingResponse>(uri, cancellationToken);

        var result = response?.Results?.FirstOrDefault();
        if (result is null)
        {
            return null;
        }

        return new CityLocation(
            Name: result.Name,
            Country: result.Country,
            Latitude: result.Latitude,
            Longitude: result.Longitude);
    }

    private static string DescribeWeatherCode(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        51 or 53 or 55 => "Drizzle",
        61 or 63 or 65 => "Rain",
        66 or 67 => "Freezing rain",
        71 or 73 or 75 => "Snow",
        77 => "Snow grains",
        80 or 81 or 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Unknown"
    };

    private sealed record CityLocation(string Name, string Country, double Latitude, double Longitude);

    private sealed class GeocodingResponse
    {
        [JsonPropertyName("results")]
        public List<GeocodingResult>? Results { get; init; }
    }

    private sealed class GeocodingResult
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; init; } = string.Empty;

        [JsonPropertyName("latitude")]
        public double Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; init; }
    }

    private sealed class ForecastResponse
    {
        [JsonPropertyName("daily")]
        public DailyData? Daily { get; init; }
    }

    private sealed class DailyData
    {
        [JsonPropertyName("time")]
        public string[] Time { get; init; } = [];

        [JsonPropertyName("weather_code")]
        public int[] WeatherCode { get; init; } = [];

        [JsonPropertyName("temperature_2m_min")]
        public double[] TemperatureMin { get; init; } = [];

        [JsonPropertyName("temperature_2m_max")]
        public double[] TemperatureMax { get; init; } = [];

        [JsonPropertyName("precipitation_sum")]
        public double[] PrecipitationSum { get; init; } = [];

        [JsonPropertyName("wind_speed_10m_max")]
        public double[] WindSpeedMax { get; init; } = [];
    }
}

public sealed record DailyWeather(
    string Date,
    double MinTemperatureC,
    double MaxTemperatureC,
    double PrecipitationMm,
    double MaxWindSpeedKmh,
    string Condition);

public sealed record WeatherForecastResult(
    string City,
    string Country,
    double Latitude,
    double Longitude,
    IReadOnlyList<DailyWeather> Days);
