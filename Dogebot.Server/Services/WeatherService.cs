using Dogebot.Server.Models;
using System.Text.Json;

namespace Dogebot.Server.Services;

public class WeatherService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<WeatherService> logger) : IWeatherService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string? _apiKey = Environment.GetEnvironmentVariable("WEATHER_API_KEY") ?? configuration["Weather:ApiKey"];

    public async Task<GeocodingResponse?> GetCityCoordinatesAsync(string cityName)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            logger.LogError("[WEATHER] API key is not configured");
            return null;
        }

        try
        {
            var encodedCityName = Uri.EscapeDataString(cityName);
            
            // First, try searching in Korea only
            var koreaUrl = $"http://api.openweathermap.org/geo/1.0/direct?q={encodedCityName},KR&limit=5&appid={_apiKey}";
            var koreaResult = await TryGetGeocodingResultAsync(koreaUrl, cityName);
            
            if (koreaResult != null)
            {
                logger.LogInformation("[WEATHER] Found result in Korea: {Name}", koreaResult.Name);
                return koreaResult;
            }

            // Fallback to global search
            logger.LogInformation("[WEATHER] No result found in Korea, falling back to global search for {CityName}", cityName);
            var globalUrl = $"http://api.openweathermap.org/geo/1.0/direct?q={encodedCityName}&limit=5&appid={_apiKey}";
            return await TryGetGeocodingResultAsync(globalUrl, cityName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WEATHER] Error fetching geocoding data for {CityName}", cityName);
            return null;
        }
    }

    private async Task<GeocodingResponse?> TryGetGeocodingResultAsync(string url, string cityName)
    {
        var response = await _httpClient.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("[WEATHER] Geocoding API request failed with status code {StatusCode}", response.StatusCode);
            return null;
        }

        var content = await response.Content.ReadAsStringAsync();
        var geocodingData = JsonSerializer.Deserialize<List<GeocodingResponse>>(content);

        if (geocodingData == null || geocodingData.Count == 0)
            return null;

        // Priority 1: Check for exact match with administrative suffixes in Korean name
        var administrativeSuffixes = new[] { "시", "군", "구", "동", "면", "읍", "리" };
        
        foreach (var suffix in administrativeSuffixes)
        {
            var cityNameWithSuffix = cityName + suffix;
            var exactMatchWithSuffix = geocodingData.FirstOrDefault(g => g.LocalNames?.GetValueOrDefault("ko")?.Equals(cityNameWithSuffix, StringComparison.OrdinalIgnoreCase) == true);
            
            if (exactMatchWithSuffix != null)
            {
                logger.LogInformation("[WEATHER] Found exact match with '{Suffix}' suffix: {Name}", suffix, exactMatchWithSuffix.LocalNames?.GetValueOrDefault("ko"));
                return exactMatchWithSuffix;
            }
        }

        // Priority 2: Check for exact match in Korean name
        var exactMatch = geocodingData.FirstOrDefault(g => g.LocalNames?.GetValueOrDefault("ko")?.Equals(cityName, StringComparison.OrdinalIgnoreCase) == true);
        
        if (exactMatch != null)
        {
            logger.LogInformation("[WEATHER] Found exact match: {Name}", exactMatch.LocalNames?.GetValueOrDefault("ko"));
            return exactMatch;
        }

        // Priority 3: Return first result
        logger.LogInformation("[WEATHER] Using first result: {Name}", geocodingData[0].Name);
        return geocodingData[0];
    }

    public async Task<WeatherResponse?> GetWeatherByCoordinatesAsync(double lat, double lon)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            logger.LogError("[WEATHER] API key is not configured");
            return null;
        }

        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={_apiKey}&units=metric&lang=kr";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("[WEATHER] Weather API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var weatherData = JsonSerializer.Deserialize<WeatherResponse>(content);

            return weatherData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WEATHER] Error fetching weather data for coordinates ({Lat}, {Lon})", lat, lon);
            return null;
        }
    }

    public async Task<ForecastResponse?> GetForecastByCoordinatesAsync(double lat, double lon)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            logger.LogError("[WEATHER] API key is not configured");
            return null;
        }

        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/forecast?lat={lat}&lon={lon}&appid={_apiKey}&units=metric&lang=kr";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("[WEATHER] Forecast API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var forecastData = JsonSerializer.Deserialize<ForecastResponse>(content);

            return forecastData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WEATHER] Error fetching forecast data for coordinates ({Lat}, {Lon})", lat, lon);
            return null;
        }
    }

    public async Task<WeatherResponse?> GetWeatherAsync(string city = "Seoul")
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            logger.LogError("[WEATHER] API key is not configured");
            return null;
        }

        try
        {
            var url = $"https://api.openweathermap.org/data/2.5/weather?q={city},KR&appid={_apiKey}&units=metric&lang=kr";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("[WEATHER] API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync();
            var weatherData = JsonSerializer.Deserialize<WeatherResponse>(content);

            return weatherData;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WEATHER] Error fetching weather data for {City}", city);
            return null;
        }
    }

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_weather", "Get current weather for a city. Defaults to Seoul when city is omitted.", CreateCitySchema()),
        new("get_weather_forecast", "Get tomorrow's weather forecast for a city. Defaults to Seoul when city is omitted.", CreateCitySchema())
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        var cityName = DengAiToolJson.ReadString(arguments, "city");
        if (string.IsNullOrWhiteSpace(cityName)) cityName = "서울";

        return toolName switch
        {
            "get_weather" => await CreateCurrentWeatherToolResultAsync(cityName),
            "get_weather_forecast" => await CreateTomorrowWeatherToolResultAsync(cityName),
            _ => "Unknown weather tool."
        };
    }

    private async Task<string> CreateCurrentWeatherToolResultAsync(string cityName)
    {
        var geocodingResponse = await GetCityCoordinatesAsync(cityName);
        if (geocodingResponse == null) return $"'{cityName}' 도시를 찾을 수 없습니다.";

        var weatherResponse = await GetWeatherByCoordinatesAsync(geocodingResponse.Lat, geocodingResponse.Lon);
        if (weatherResponse == null) return $"'{cityName}' 날씨 정보를 가져올 수 없습니다.";

        var weatherEmoji = GetWeatherEmoji(weatherResponse.Weather.FirstOrDefault()?.Main ?? string.Empty);
        var description = weatherResponse.Weather.FirstOrDefault()?.Description ?? "정보 없음";
        var displayCityName = geocodingResponse.LocalNames?.GetValueOrDefault("ko") ?? weatherResponse.Name;

        return $"{weatherEmoji} {displayCityName} 날씨\n\n" +
            $"현재 기온: {weatherResponse.Main.Temp:F1}°C\n" +
            $"체감 온도: {weatherResponse.Main.FeelsLike:F1}°C\n" +
            $"날씨: {description}\n" +
            $"습도: {weatherResponse.Main.Humidity}%\n" +
            $"풍속: {weatherResponse.Wind.Speed:F1}m/s\n" +
            $"기압: {weatherResponse.Main.Pressure}hPa";
    }

    private async Task<string> CreateTomorrowWeatherToolResultAsync(string cityName)
    {
        var geocodingResponse = await GetCityCoordinatesAsync(cityName);
        if (geocodingResponse == null) return $"'{cityName}' 도시를 찾을 수 없습니다.";

        var forecastResponse = await GetForecastByCoordinatesAsync(geocodingResponse.Lat, geocodingResponse.Lon);
        if (forecastResponse == null || forecastResponse.List.Count == 0) return $"'{cityName}' 날씨 예보를 가져올 수 없습니다.";

        var tomorrow = DateTime.Now.AddDays(1).Date.AddHours(12);
        var tomorrowForecasts = forecastResponse.List
            .Where(forecast => DateTimeOffset.FromUnixTimeSeconds(forecast.Dt).LocalDateTime.Date == tomorrow.Date)
            .ToList();
        if (tomorrowForecasts.Count == 0) return $"'{cityName}' 내일 날씨 정보가 없습니다.";

        var minimumTemperature = tomorrowForecasts.Min(forecast => forecast.Main.TempMin);
        var maximumTemperature = tomorrowForecasts.Max(forecast => forecast.Main.TempMax);
        var noonForecast = tomorrowForecasts
            .OrderBy(forecast => Math.Abs((DateTimeOffset.FromUnixTimeSeconds(forecast.Dt).LocalDateTime - tomorrow).TotalHours))
            .First();
        var weatherEmoji = GetWeatherEmoji(noonForecast.Weather.FirstOrDefault()?.Main ?? string.Empty);
        var description = noonForecast.Weather.FirstOrDefault()?.Description ?? "정보 없음";
        var displayCityName = geocodingResponse.LocalNames?.GetValueOrDefault("ko") ?? geocodingResponse.Name;

        return $"{weatherEmoji} {displayCityName} 내일 날씨\n\n" +
            $"최저/최고 기온: {minimumTemperature:F1}°C / {maximumTemperature:F1}°C\n" +
            $"날씨: {description}\n" +
            $"습도: {noonForecast.Main.Humidity}%\n" +
            $"풍속: {noonForecast.Wind.Speed:F1}m/s\n" +
            $"기압: {noonForecast.Main.Pressure}hPa";
    }

    private static string GetWeatherEmoji(string weatherMain) =>
        weatherMain.ToLower() switch
        {
            "clear" => "☀️",
            "clouds" => "☁️",
            "rain" => "🌧️",
            "drizzle" => "🌦️",
            "thunderstorm" => "⛈️",
            "snow" => "🌨️",
            "mist" or "fog" or "haze" => "🌫️",
            _ => "🌤️"
        };

    private static DengAiJsonSchema CreateCitySchema() =>
        DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["city"] = DengAiJsonSchemaProperty.String("Korean city name to query. Use Seoul when the user did not specify a city.")
        });

    #endregion
}
