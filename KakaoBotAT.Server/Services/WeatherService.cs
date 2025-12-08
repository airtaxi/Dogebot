using KakaoBotAT.Server.Models;
using System.Text.Json;

namespace KakaoBotAT.Server.Services;

public interface IWeatherService
{
    Task<WeatherResponse?> GetWeatherAsync(string city = "Seoul");
    Task<WeatherResponse?> GetWeatherByCoordinatesAsync(double lat, double lon);
    Task<ForecastResponse?> GetForecastByCoordinatesAsync(double lat, double lon);
    Task<GeocodingResponse?> GetCityCoordinatesAsync(string cityName);
}

public class WeatherService : IWeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherService> _logger;
    private readonly string? _apiKey;

    public WeatherService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<WeatherService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
        _apiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY") ?? configuration["Weather:ApiKey"];
    }

    public async Task<GeocodingResponse?> GetCityCoordinatesAsync(string cityName)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("[WEATHER] API key is not configured");
            return null;
        }

        try
        {
            var encodedCityName = Uri.EscapeDataString(cityName);
            var url = $"http://api.openweathermap.org/geo/1.0/direct?q={encodedCityName}&limit=1&appid={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[WEATHER] Geocoding API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var geocodingData = JsonSerializer.Deserialize<List<GeocodingResponse>>(content);

            return geocodingData?.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WEATHER] Error fetching geocoding data for {CityName}", cityName);
            return null;
        }
    }

    public async Task<WeatherResponse?> GetWeatherByCoordinatesAsync(double lat, double lon)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("[WEATHER] API key is not configured");
            return null;
        }

        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={_apiKey}&units=metric&lang=kr";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[WEATHER] Weather API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var weatherData = JsonSerializer.Deserialize<WeatherResponse>(content);

            return weatherData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WEATHER] Error fetching weather data for coordinates ({Lat}, {Lon})", lat, lon);
            return null;
        }
    }

    public async Task<ForecastResponse?> GetForecastByCoordinatesAsync(double lat, double lon)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("[WEATHER] API key is not configured");
            return null;
        }

        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/forecast?lat={lat}&lon={lon}&appid={_apiKey}&units=metric&lang=kr";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[WEATHER] Forecast API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var forecastData = JsonSerializer.Deserialize<ForecastResponse>(content);

            return forecastData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WEATHER] Error fetching forecast data for coordinates ({Lat}, {Lon})", lat, lon);
            return null;
        }
    }

    public async Task<WeatherResponse?> GetWeatherAsync(string city = "Seoul")
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogError("[WEATHER] API key is not configured");
            return null;
        }

        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={city},KR&appid={_apiKey}&units=metric&lang=kr";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[WEATHER] API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var weatherData = JsonSerializer.Deserialize<WeatherResponse>(content);

            return weatherData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WEATHER] Error fetching weather data for {City}", city);
            return null;
        }
    }
}
