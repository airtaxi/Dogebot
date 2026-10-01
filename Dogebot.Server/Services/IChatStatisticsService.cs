using Dogebot.Commons;

namespace Dogebot.Server.Services;

public interface IChatStatisticsService : IDengAiCallableService
{
    Task RecordMessageAsync(KakaoMessageData data);
    Task<List<string>> GetKnownSenderNamesAsync(string roomId, string excludedSenderHash, long? minimumLastMessageTimeMilliseconds = null);
    Task<List<string>> GetSenderHashesByNameAsync(string roomId, string senderName);
    Task<List<(string SenderName, long MessageCount)>> GetTopUsersAsync(string roomId, int limit = 10);
    Task<(int Rank, long MessageCount)?> GetUserRankAsync(string roomId, string senderHash);
    /// <summary>
    /// Gets per-user message counts within the specified UTC time range, ordered by count descending.
    /// </summary>
    Task<List<(string SenderName, long MessageCount)>> GetTopUsersByPeriodAsync(string roomId, DateTimeOffset fromUtc, DateTimeOffset toUtc, int limit = 10);
    /// <summary>
    /// Gets the rank and message count of a specific user within the specified UTC time range.
    /// </summary>
    Task<(int Rank, long MessageCount)?> GetUserRankByPeriodAsync(string roomId, string senderHash, DateTimeOffset fromUtc, DateTimeOffset toUtc);
    Task<List<(string Content, long Count)>> GetTopMessagesAsync(string roomId, int limit = 10);
    Task<List<(string Word, long Count)>> GetTopWordsAsync(string roomId, int limit = 10);
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
    /// <summary>
    /// Gets message counts grouped by day of week for a room.
    /// </summary>
    Task<List<(DayOfWeek Day, long MessageCount)>> GetDailyStatisticsAsync(string roomId);
    /// <summary>
    /// Gets message counts grouped by day of week for a specific user in a room.
    /// </summary>
    Task<List<(DayOfWeek Day, long MessageCount)>> GetUserDailyStatisticsAsync(string roomId, string senderHash);
    /// <summary>
    /// Gets message counts grouped by month (1-12) for a room.
    /// </summary>
    Task<List<(int Month, long MessageCount)>> GetMonthlyStatisticsAsync(string roomId);
    /// <summary>
    /// Gets message counts grouped by month (1-12) for a specific user in a room.
    /// </summary>
    Task<List<(int Month, long MessageCount)>> GetUserMonthlyStatisticsAsync(string roomId, string senderHash);
}

