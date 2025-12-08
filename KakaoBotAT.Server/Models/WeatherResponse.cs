using System.Text.Json.Serialization;

namespace KakaoBotAT.Server.Models;

public class WeatherResponse
{
    [JsonPropertyName("weather")]
    public List<Weather> Weather { get; set; } = new();

    [JsonPropertyName("main")]
    public MainWeather Main { get; set; } = new();

    [JsonPropertyName("wind")]
    public Wind Wind { get; set; } = new();

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class Weather
{
    [JsonPropertyName("main")]
    public string Main { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;
}

public class MainWeather
{
    [JsonPropertyName("temp")]
    public double Temp { get; set; }

    [JsonPropertyName("feels_like")]
    public double FeelsLike { get; set; }

    [JsonPropertyName("humidity")]
    public int Humidity { get; set; }

    [JsonPropertyName("pressure")]
    public int Pressure { get; set; }
}

public class Wind
{
    [JsonPropertyName("speed")]
    public double Speed { get; set; }
}

public class GeocodingResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("local_names")]
    public Dictionary<string, string>? LocalNames { get; set; }
}
