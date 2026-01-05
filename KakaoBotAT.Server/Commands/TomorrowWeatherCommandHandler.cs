using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !내일날씨 [지역명] command to show tomorrow's weather forecast.
/// Uses OpenWeatherMap API. If no city is specified, uses the user's preferred city or defaults to Seoul.
/// </summary>
public class TomorrowWeatherCommandHandler : ICommandHandler
{
    private readonly IWeatherService _weatherService;
    private readonly IUserPreferenceService _userPreferenceService;
    private readonly ILogger<TomorrowWeatherCommandHandler> _logger;

    public TomorrowWeatherCommandHandler(
        IWeatherService weatherService,
        IUserPreferenceService userPreferenceService,
        ILogger<TomorrowWeatherCommandHandler> logger)
    {
        _weatherService = weatherService;
        _userPreferenceService = userPreferenceService;
        _logger = logger;
    }

    public string Command => "!내일날씨";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            // Parse city name
            var content = data.Content.Trim();
            string? cityName = null;
            bool userSpecifiedCity = false;
            
            if (content.Length > Command.Length)
            {
                var inputCity = content.Substring(Command.Length).Trim();
                if (!string.IsNullOrEmpty(inputCity))
                {
                    cityName = inputCity;
                    userSpecifiedCity = true;
                }
            }

            // If no city specified, try to get user's preferred city
            if (cityName == null)
            {
                cityName = await _userPreferenceService.GetUserPreferredCityAsync(data.SenderHash);
                
                // If no preference found, use default
                if (cityName == null)
                {
                    cityName = "서울";
                }
            }

            // Get city information using Geocoding API
            var geoData = await _weatherService.GetCityCoordinatesAsync(cityName);

            if (geoData == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ '{cityName}' 도시를 찾을 수 없습니다.\n다른 도시명으로 시도해주세요."
                };
            }

            // If user specified a city, save it as their preference
            if (userSpecifiedCity)
            {
                await _userPreferenceService.SetUserPreferredCityAsync(data.SenderHash, cityName);
            }

            // Get forecast using coordinates
            var forecast = await _weatherService.GetForecastByCoordinatesAsync(geoData.Lat, geoData.Lon);

            if (forecast == null || forecast.List.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ '{cityName}' 날씨 예보를 가져올 수 없습니다."
                };
            }

            // Get tomorrow's date (next day at noon)
            var tomorrow = DateTime.Now.AddDays(1).Date.AddHours(12);
            
            // Find forecast items for tomorrow
            var tomorrowForecasts = forecast.List
                .Where(f => DateTimeOffset.FromUnixTimeSeconds(f.Dt).LocalDateTime.Date == tomorrow.Date)
                .ToList();

            if (!tomorrowForecasts.Any())
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ '{cityName}' 내일 날씨 정보가 없습니다."
                };
            }

            // Get min and max temperatures for tomorrow
            var minTemp = tomorrowForecasts.Min(f => f.Main.TempMin);
            var maxTemp = tomorrowForecasts.Max(f => f.Main.TempMax);

            // Get the forecast closest to noon (12:00)
            var noonForecast = tomorrowForecasts
                .OrderBy(f => Math.Abs((DateTimeOffset.FromUnixTimeSeconds(f.Dt).LocalDateTime - tomorrow).TotalHours))
                .First();

            var weatherEmoji = GetWeatherEmoji(noonForecast.Weather.FirstOrDefault()?.Main ?? "");
            var description = noonForecast.Weather.FirstOrDefault()?.Description ?? "정보 없음";
            
            // Prefer Korean city name, fallback to English
            var displayCityName = geoData.LocalNames?.GetValueOrDefault("ko") ?? geoData.Name;
            
            var message = $"{weatherEmoji} {displayCityName} 내일 날씨\n\n" +
                         $"🌡️ 최저/최고 기온: {minTemp:F1}°C / {maxTemp:F1}°C\n" +
                         $"☁️ 날씨: {description}\n" +
                         $"💧 습도: {noonForecast.Main.Humidity}%\n" +
                         $"🌬️ 풍속: {noonForecast.Wind.Speed:F1}m/s\n" +
                         $"🔽 기압: {noonForecast.Main.Pressure}hPa";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[WEATHER] Tomorrow's weather info requested by {Sender} in room {RoomId} for {City}: {MinTemp}°C~{MaxTemp}°C, {Description}",
                    data.SenderName, data.RoomId, displayCityName, minTemp, maxTemp, description);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WEATHER] Error processing tomorrow weather command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "내일 날씨 조회 중 오류가 발생했습니다."
            };
        }
    }

    private static string GetWeatherEmoji(string weatherMain)
    {
        return weatherMain.ToLower() switch
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
    }
}
