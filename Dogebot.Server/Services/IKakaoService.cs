using Dogebot.Commons;

namespace Dogebot.Server.Services;

public interface IKakaoService
{
    /// <summary>
    /// Processes notification messages received from the client and generates an immediate response.
    /// This method is used to respond synchronously to commands (!ping) included in the notification.
    /// </summary>
    /// <param name="notification">The received notification data.</param>
    /// <returns>Action to be performed by the client on KakaoTalk (reply, read, etc.).</returns>
    Task<ServerResponse> HandleNotificationAsync(ServerNotification notification);

    /// <summary>
    /// Retrieves queued commands from the server for the client. (For polling)
    /// Checks for due scheduled messages and pending IMAX notifications for rooms
    /// where the client currently has reply actions available.
    /// </summary>
    /// <param name="availableRoomIds">Room IDs where the client has active reply actions.</param>
    /// <returns>Command to execute or empty command.</returns>
    Task<ServerResponse> GetPendingCommandAsync(IEnumerable<string> availableRoomIds);
}
