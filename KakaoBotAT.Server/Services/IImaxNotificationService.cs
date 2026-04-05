using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;

namespace KakaoBotAT.Server.Services;

public enum ImaxSessionType
{
    Setup,
    ScheduleQuery,
    MovieList
}

public interface IImaxNotificationService
{
    /// <summary>
    /// Starts a multi-stage session for IMAX-related operations (setup, schedule query, or movie list).
    /// </summary>
    void StartSession(string roomId, string senderHash, string senderName, string roomName,
        ImaxSessionType type = ImaxSessionType.Setup, string? movieSearchQuery = null);

    /// <summary>
    /// Handles input from a user who has an active setup session.
    /// Returns null if no active session exists or input should be passed to command routing.
    /// </summary>
    Task<ServerResponse?> HandleSessionInputAsync(KakaoMessageData data);

    /// <summary>
    /// Registers an IMAX notification for a room. Only one notification per room is allowed.
    /// </summary>
    Task<(bool Success, string Message)> RegisterAsync(
        string roomId, string screeningDate, string movieName, string movieNumber,
        string siteNumber, string siteName, string? keyword,
        string senderHash, string senderName, string roomName);

    /// <summary>
    /// Gets the active IMAX notification for a room, or null if none exists.
    /// </summary>
    Task<ImaxNotification?> GetNotificationAsync(string roomId);

    /// <summary>
    /// Gets all active IMAX notifications across all rooms.
    /// </summary>
    Task<List<ImaxNotification>> GetAllActiveNotificationsAsync();

    /// <summary>
    /// Removes the IMAX notification for a room.
    /// </summary>
    Task<bool> RemoveNotificationAsync(string roomId);

    /// <summary>
    /// Sets the pending message for a notification (called by background check service when IMAX is detected).
    /// </summary>
    Task SetPendingMessageAsync(string notificationId, string message);

    /// <summary>
    /// Checks if there's a pending IMAX notification for the room and delivers it.
    /// The notification is atomically deleted upon delivery (one-time notification).
    /// Returns null if no pending notification exists.
    /// </summary>
    Task<ServerResponse?> CheckAndDeliverAsync(KakaoMessageData data);

    /// <summary>
    /// Checks for pending IMAX notifications in any of the given rooms and delivers the first one.
    /// Used by the polling endpoint for proactive delivery when the client has reply actions available.
    /// The notification is atomically deleted upon delivery (one-time notification).
    /// Returns null if no pending notification exists for any of the given rooms.
    /// </summary>
    Task<ServerResponse?> CheckAndDeliverForRoomsAsync(IEnumerable<string> roomIds);

    /// <summary>
    /// Deletes notifications whose screening date has passed (KST).
    /// </summary>
    Task<int> CleanupExpiredNotificationsAsync();

    /// <summary>
    /// Cleans up expired setup sessions (older than 5 minutes).
    /// </summary>
    int CleanupExpiredSessions();
}
