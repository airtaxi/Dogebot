using KakaoBotAT.Server.Models;
using System.Text.Json;

namespace KakaoBotAT.Server.Services;

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
            var url = $"http://api.openweathermap.org/geo/1.0/direct?q={encodedCityName}&limit=5&appid={_apiKey}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[WEATHER] Geocoding API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var geocodingData = JsonSerializer.Deserialize<List<GeocodingResponse>>(content);

            if (geocodingData == null || geocodingData.Count == 0)
                return null;

            // Priority 1: Check for exact match with administrative suffixes in Korean name
            var administrativeSuffixes = new[] { "시", "군", "구", "면", "읍", "동" };
            
            foreach (var suffix in administrativeSuffixes)
            {
                var cityNameWithSuffix = cityName + suffix;
                var exactMatchWithSuffix = geocodingData.FirstOrDefault(g => 
                    g.LocalNames?.GetValueOrDefault("ko")?.Equals(cityNameWithSuffix, StringComparison.OrdinalIgnoreCase) == true);
                
                if (exactMatchWithSuffix != null)
                {
                    _logger.LogInformation("[WEATHER] Found exact match with '{Suffix}' suffix: {Name}", suffix, exactMatchWithSuffix.LocalNames?.GetValueOrDefault("ko"));
                    return exactMatchWithSuffix;
                }
            }

            // Priority 2: Check for exact match in Korean name
            var exactMatch = geocodingData.FirstOrDefault(g => 
                g.LocalNames?.GetValueOrDefault("ko")?.Equals(cityName, StringComparison.OrdinalIgnoreCase) == true);
            
            if (exactMatch != null)
            {
                _logger.LogInformation("[WEATHER] Found exact match: {Name}", exactMatch.LocalNames?.GetValueOrDefault("ko"));
                return exactMatch;
            }

            // Priority 3: Return first result
            _logger.LogInformation("[WEATHER] Using first result: {Name}", geocodingData[0].Name);
            return geocodingData[0];
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
