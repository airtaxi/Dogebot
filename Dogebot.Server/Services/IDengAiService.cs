using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IDengAiService
{
    bool IsConfigured { get; }

    void RecordRoomMessage(string roomId, string senderName, string content);

    Task<string?> GenerateReplyAsync(string userMessage, DengAiToolContext? toolContext = null, bool isAdmin = false, CancellationToken cancellationToken = default);
}
