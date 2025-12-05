using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Interface for command handlers.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Gets the command that this handler processes (e.g., "!핑").
    /// </summary>
    string Command { get; }

    /// <summary>
    /// Determines whether this handler can process the given message content.
    /// </summary>
    bool CanHandle(string content);

    /// <summary>
    /// Handles the command and returns a response.
    /// </summary>
    Task<ServerResponse> HandleAsync(KakaoMessageData data);
}
