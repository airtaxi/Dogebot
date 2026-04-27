using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !날씨 [지역명] command to show current weather.
/// Uses OpenWeatherMap API. If no city is specified, uses the user's preferred city or defaults to Seoul.
/// </summary>
public class WeatherCommandHandler(
    IWeatherService weatherService,
    IUserPreferenceService userPreferenceService,
    ILogger<WeatherCommandHandler> logger) : ICommandHandler
{
    public string Command => "!날씨";

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
                cityName = await userPreferenceService.GetUserPreferredCityAsync(data.SenderHash);
                
                // If no preference found, use default
                if (cityName == null)
                {
                    cityName = "서울";
                }
            }

            // Get city information using Geocoding API
            var geoData = await weatherService.GetCityCoordinatesAsync(cityName);

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
                await userPreferenceService.SetUserPreferredCityAsync(data.SenderHash, cityName);
            }

            // Get weather using coordinates (more accurate and stable)
            var weather = await weatherService.GetWeatherByCoordinatesAsync(geoData.Lat, geoData.Lon);

            if (weather == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ '{cityName}' 날씨 정보를 가져올 수 없습니다."
                };
            }

            var weatherEmoji = GetWeatherEmoji(weather.Weather.FirstOrDefault()?.Main ?? "");
            var description = weather.Weather.FirstOrDefault()?.Description ?? "정보 없음";
            
            // Prefer Korean city name, fallback to English
            var displayCityName = geoData.LocalNames?.GetValueOrDefault("ko") ?? weather.Name;
            
            var message = $"{weatherEmoji} {displayCityName} 날씨\n\n" +
                         $"🌡️ 현재 기온: {weather.Main.Temp:F1}°C\n" +
                         $"🤔 체감 온도: {weather.Main.FeelsLike:F1}°C\n" +
                         $"☁️ 날씨: {description}\n" +
                         $"💧 습도: {weather.Main.Humidity}%\n" +
                         $"🌬️ 풍속: {weather.Wind.Speed:F1}m/s\n" +
                         $"🔽 기압: {weather.Main.Pressure}hPa";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[WEATHER] Weather info requested by {Sender} in room {RoomId} for {City}: {Temp}°C, {Description}",
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
            logger.LogError(ex, "[WEATHER] Error processing weather command");
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

