using KakaoBotAT.Server.Models;

namespace KakaoBotAT.Server.Services;

public interface IWeatherService
{
    Task<WeatherResponse?> GetWeatherAsync(string city = "Seoul");
    Task<WeatherResponse?> GetWeatherByCoordinatesAsync(double lat, double lon);
    Task<ForecastResponse?> GetForecastByCoordinatesAsync(double lat, double lon);
    Task<GeocodingResponse?> GetCityCoordinatesAsync(string cityName);
}
