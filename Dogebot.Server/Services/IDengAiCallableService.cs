namespace Dogebot.Server.Services;

public interface IDengAiCallableService
{
    IReadOnlyList<DengAiToolDefinition> GetDengAiTools();
    Task<string> ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken = default);
}
