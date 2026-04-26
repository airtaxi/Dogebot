using KakaoBotAT.Server.Models;

namespace KakaoBotAT.Server.Services;

public interface IUserPreferenceService
{
    Task<string?> GetUserPreferredCityAsync(string senderHash);
    Task SetUserPreferredCityAsync(string senderHash, string cityName);
}
