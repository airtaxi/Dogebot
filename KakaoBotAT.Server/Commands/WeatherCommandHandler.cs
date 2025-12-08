using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !날씨 [지역명] command to show current weather.
/// Uses OpenWeatherMap API. Default city is Seoul.
/// </summary>
public class WeatherCommandHandler : ICommandHandler
{
    private readonly IWeatherService _weatherService;
    private readonly ILogger<WeatherCommandHandler> _logger;

    public WeatherCommandHandler(
        IWeatherService weatherService,
        ILogger<WeatherCommandHandler> logger)
    {
        _weatherService = weatherService;
        _logger = logger;
    }

    public string Command => "!날씨";

    public bool CanHandle(string content)
    {
        return content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            // 지역명 파싱 (기본값: 서울)
            var content = data.Content.Trim();
            var cityName = "서울";
            
            if (content.Length > Command.Length)
            {
                var inputCity = content.Substring(Command.Length).Trim();
                if (!string.IsNullOrEmpty(inputCity))
                {
                    cityName = inputCity;
                }
            }

            // Geocoding API를 사용하여 도시 정보 조회
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

            // 영어 도시명으로 날씨 조회
            var weather = await _weatherService.GetWeatherAsync(geoData.Name);

            if (weather == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ '{cityName}' 날씨 정보를 가져올 수 없습니다.\n지원하는 도시명인지 확인해주세요."
                };
            }

            var weatherEmoji = GetWeatherEmoji(weather.Weather.FirstOrDefault()?.Main ?? "");
            var description = weather.Weather.FirstOrDefault()?.Description ?? "정보 없음";
            
            // 한국어 도시명 우선 사용, 없으면 영어 도시명 사용
            var displayCityName = geoData.LocalNames?.GetValueOrDefault("ko") ?? weather.Name;
            
            var message = $"{weatherEmoji} {displayCityName} 날씨\n\n" +
                         $"🌡️ 현재 기온: {weather.Main.Temp:F1}°C\n" +
                         $"🤔 체감 온도: {weather.Main.FeelsLike:F1}°C\n" +
                         $"☁️ 날씨: {description}\n" +
                         $"💧 습도: {weather.Main.Humidity}%\n" +
                         $"🌬️ 풍속: {weather.Wind.Speed:F1}m/s\n" +
                         $"🔽 기압: {weather.Main.Pressure}hPa";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[WEATHER] Weather info requested by {Sender} in room {RoomId} for {City}: {Temp}°C, {Description}",
                    data.SenderName, data.RoomId, displayCityName, weather.Main.Temp, description);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WEATHER] Error processing weather command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "날씨 조회 중 오류가 발생했습니다."
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
