using Dogebot.Commons;
using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IScheduledMessageService
{
    /// <summary>
    /// Starts a multi-stage session for setting up a scheduled message.
    /// </summary>
    void StartSession(string roomId, string senderHash, string senderName, string roomName);

    /// <summary>
    /// Handles input from a user who has an active setup session.
    /// Returns null if no active session exists or input should be passed to command routing.
    /// </summary>
    Task<ServerResponse?> HandleSessionInputAsync(KakaoMessageData data);

    /// <summary>
    /// Checks whether a scheduled message should be triggered for the given room and returns it.
    /// Returns null if no scheduled message should be sent at this time.
    /// </summary>
    Task<ServerResponse?> CheckAndSendScheduledMessageAsync(KakaoMessageData data);

    /// <summary>
    /// Checks whether scheduled messages should be triggered for the given room and returns all due response items.
    /// </summary>
    Task<List<ServerResponseItem>> CheckAndSendScheduledMessagesAsync(KakaoMessageData data);

    /// <summary>
    /// Gets all scheduled messages for a room.
    /// </summary>
    Task<List<ScheduledMessage>> GetScheduledMessagesAsync(string roomId);

    /// <summary>
    /// Removes a scheduled message by its 1-based display index for the given room.
    /// </summary>
    Task<bool> RemoveScheduledMessageAsync(string roomId, int displayIndex);

    /// <summary>
    /// Removes all scheduled messages for a room.
    /// </summary>
    Task<int> RemoveAllScheduledMessagesAsync(string roomId);

    /// <summary>
    /// Checks whether scheduled messages should be triggered for any of the given rooms and returns one.
    /// Used by the polling endpoint for proactive delivery when the client has reply actions available.
    /// Returns null if no scheduled message should be sent at this time.
    /// </summary>
    Task<ServerResponse?> CheckAndSendScheduledMessageForRoomsAsync(IEnumerable<string> roomIds);

    /// <summary>
    /// Checks whether scheduled messages should be triggered for any of the given rooms and returns all due response items.
    /// </summary>
    Task<List<ServerResponseItem>> CheckAndSendScheduledMessagesForRoomsAsync(IEnumerable<string> roomIds);

    /// <summary>
    /// Cleans up expired setup sessions (older than 5 minutes).
    /// </summary>
    int CleanupExpiredSessions();

    /// <summary>
    /// Cleans up stale sent-tracking entries from previous days.
    /// </summary>
    int CleanupStaleSentTracking();
}

