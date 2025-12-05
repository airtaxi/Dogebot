using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !날씨 command to show current weather in Seoul.
/// Uses OpenWeatherMap API.
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
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var weather = await _weatherService.GetWeatherAsync("Seoul");

            if (weather == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 날씨 정보를 가져올 수 없습니다.\nAPI 키가 설정되어 있는지 확인해주세요."
                };
            }

            var weatherEmoji = GetWeatherEmoji(weather.Weather.FirstOrDefault()?.Main ?? "");
            var description = weather.Weather.FirstOrDefault()?.Description ?? "정보 없음";
            
            var message = $"{weatherEmoji} 서울특별시 날씨\n\n" +
                         $"🌡️ 현재 기온: {weather.Main.Temp:F1}°C\n" +
                         $"🤔 체감 온도: {weather.Main.FeelsLike:F1}°C\n" +
                         $"☁️ 날씨: {description}\n" +
                         $"💧 습도: {weather.Main.Humidity}%\n" +
                         $"🌬️ 풍속: {weather.Wind.Speed:F1}m/s\n" +
                         $"🔽 기압: {weather.Main.Pressure}hPa";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[WEATHER] Weather info requested by {Sender} in room {RoomId}: {Temp}°C, {Description}",
                    data.SenderName, data.RoomId, weather.Main.Temp, description);

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
