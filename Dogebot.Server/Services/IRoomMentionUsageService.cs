namespace Dogebot.Server.Services;

public interface IRoomMentionUsageService
{
    Task<(bool CanUse, long NextAvailableAt)> TryUseAsync(string roomId, string roomName, string senderHash, string senderName, long currentUnixTimeSeconds);
}
