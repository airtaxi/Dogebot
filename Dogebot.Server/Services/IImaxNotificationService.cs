using Dogebot.Commons;
using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public enum ImaxSessionType
{
    Setup,
    ScheduleQuery,
    MovieList
}

public interface IImaxNotificationService : IDengAiCallableService
{
    /// <summary>
    /// Starts a multi-stage session for IMAX-related operations (setup, schedule query, or movie list).
    /// </summary>
    void StartSession(string roomId, string senderHash, string senderName, string roomName, ImaxSessionType type = ImaxSessionType.Setup, string? movieSearchQuery = null);

    /// <summary>
    /// Handles input from a user who has an active setup session.
    /// Returns null if no active session exists or input should be passed to command routing.
    /// </summary>
    Task<ServerResponse?> HandleSessionInputAsync(KakaoMessageData data);

    /// <summary>
    /// Registers an IMAX notification for a room. Duplicate registrations with the same movie, site, and date are rejected.
    /// </summary>
    Task<(bool Success, string Message)> RegisterAsync(string roomId, string screeningDate, string movieName, string movieNumber, string siteNumber, string siteName, string? keyword, string senderHash, string senderName, string roomName);

    /// <summary>
    /// Gets all active IMAX notifications for a room, ordered by creation time (oldest first).
    /// </summary>
    Task<List<ImaxNotification>> GetNotificationsAsync(string roomId);

    /// <summary>
    /// Gets all active IMAX notifications across all rooms.
    /// </summary>
    Task<List<ImaxNotification>> GetAllActiveNotificationsAsync();

    /// <summary>
    /// Removes the IMAX notification at the given display index (1-based) for a room.
    /// Returns the removed notification, or null if the index is invalid.
    /// </summary>
    Task<ImaxNotification?> RemoveNotificationAsync(string roomId, int displayIndex);

    /// <summary>
    /// Removes all IMAX notifications for a room.
    /// </summary>
    Task<int> RemoveAllNotificationsAsync(string roomId);

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
    /// Checks all pending IMAX notifications for the room and delivers them.
    /// The notifications are atomically deleted upon delivery.
    /// </summary>
    Task<List<ServerResponseItem>> CheckAndDeliverManyAsync(KakaoMessageData data);

    /// <summary>
    /// Checks for pending IMAX notifications in any of the given rooms and delivers the first one.
    /// Used by the polling endpoint for proactive delivery when the client has reply actions available.
    /// The notification is atomically deleted upon delivery (one-time notification).
    /// Returns null if no pending notification exists for any of the given rooms.
    /// </summary>
    Task<ServerResponse?> CheckAndDeliverForRoomsAsync(IEnumerable<string> roomIds);

    /// <summary>
    /// Checks for all pending IMAX notifications in the given rooms and delivers them.
    /// The notifications are atomically deleted upon delivery.
    /// </summary>
    Task<List<ServerResponseItem>> CheckAndDeliverManyForRoomsAsync(IEnumerable<string> roomIds);

    /// <summary>
    /// Deletes notifications whose screening date has passed (KST).
    /// </summary>
    Task<int> CleanupExpiredNotificationsAsync();

    /// <summary>
    /// Cleans up expired setup sessions (older than 5 minutes).
    /// </summary>
    int CleanupExpiredSessions();
}

