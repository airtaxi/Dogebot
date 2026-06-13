namespace Dogebot.Server.Services;

public interface IDengAiService
{
    bool IsConfigured { get; }

    Task<string?> GenerateReplyAsync(string userMessage, CancellationToken cancellationToken = default);
}
