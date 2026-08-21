using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IUserBaseballTeamPreferenceService : IDengAiCallableService
{
    Task<string?> GetUserPreferredTeamAsync(string senderHash);
    Task<string?> GetUserPreferredTeamByNameAsync(string roomId, string senderName);
    Task SetUserPreferredTeamAsync(string senderHash, string teamName);
}
