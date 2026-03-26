using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;

namespace KakaoBotAT.Server.Services;

public interface IImaxNotificationService
{
    /// <summary>
    /// Registers an IMAX notification for a room. Only one notification per room is allowed.
    /// </summary>
    Task<(bool Success, string Message)> RegisterAsync(
        string roomId, string screeningDate, string? keyword,
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
    /// Deletes notifications whose screening date has passed (KST).
    /// </summary>
    Task<int> CleanupExpiredNotificationsAsync();
}
