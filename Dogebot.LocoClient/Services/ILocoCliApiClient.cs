using Dogebot.LocoClient.Models;

namespace Dogebot.LocoClient.Services;

public interface ILocoCliApiClient
{
    Task<IReadOnlyList<LocoRoom>> GetRoomsAsync(CancellationToken cancellationToken);

    Task SendMessageAsync(string roomId, string message, CancellationToken cancellationToken);

    /// <summary>
    /// Streams room messages until the caller cancels the operation, reconnecting automatically when the WebSocket is closed.
    /// </summary>
    Task RunRoomStreamAsync(string roomId, Func<LocoRoomMessage, LocoRoom?, Task> onMessageReceived, CancellationToken cancellationToken);
}
