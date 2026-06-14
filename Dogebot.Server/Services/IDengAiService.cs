namespace Dogebot.Server.Services;

public interface IDengAiService
{
    bool IsConfigured { get; }

    Task<string?> GenerateReplyAsync(string userMessage, DengAiToolContext? toolContext = null, CancellationToken cancellationToken = default);
}
