using KakaoBotAT.Server.Models;
using System.Text.Json;

namespace KakaoBotAT.Server.Services;

public interface IWeatherService
{
    Task<WeatherResponse?> GetWeatherAsync(string city = "Seoul");
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
