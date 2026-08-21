namespace Dogebot.Server.Services;

public interface IUserBaseballTeamPreferenceService
{
    Task<string?> GetUserPreferredTeamAsync(string senderHash);
    Task SetUserPreferredTeamAsync(string senderHash, string teamName);
}
