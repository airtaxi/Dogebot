using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Services;

public interface IChatStatisticsService
{
    Task RecordMessageAsync(KakaoMessageData data);
    Task<List<(string SenderName, long MessageCount)>> GetTopUsersAsync(string roomId, int limit = 10);
    Task<(int Rank, long MessageCount)?> GetUserRankAsync(string roomId, string senderHash);
    Task<List<(string Content, long Count)>> GetTopMessagesAsync(string roomId, int limit = 10);
    Task<(long TotalMessages, int UniqueUsers)> GetRoomStatisticsAsync(string roomId);
    Task<bool> IsMessageContentEnabledAsync(string roomId);
    Task<bool> EnableMessageContentAsync(string roomId, string roomName, string setBy);
    Task<bool> DisableMessageContentAsync(string roomId, string roomName, string setBy);
    /// <summary>
    /// Gets message counts grouped by hour (0-23) for a room.
    /// </summary>
    Task<List<(int Hour, long MessageCount)>> GetHourlyStatisticsAsync(string roomId);
    /// <summary>
    /// Gets message counts grouped by hour (0-23) for a specific user in a room.
    /// </summary>
    Task<List<(int Hour, long MessageCount)>> GetUserHourlyStatisticsAsync(string roomId, string senderHash);
}
