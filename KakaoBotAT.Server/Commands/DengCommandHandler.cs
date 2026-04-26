using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !핑 (ping) command.
/// </summary>
public class DengCommandHandler(ILogger<DengCommandHandler> logger) : ICommandHandler
{
    public string Command => "댕";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        var response = new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = "댕"
        };

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("[PING] Responding to {Command} command from {SenderName}: {Message}", 
                Command, data.SenderName, response.Message);

        return Task.FromResult(response);
    }
}
