using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IUserPreferenceService
{
    Task<string?> GetUserPreferredCityAsync(string senderHash);
    Task SetUserPreferredCityAsync(string senderHash, string cityName);
}

