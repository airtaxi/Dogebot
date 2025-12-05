namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Factory for managing and retrieving command handlers.
/// </summary>
public class CommandHandlerFactory(IEnumerable<ICommandHandler> handlers)
{
    private readonly IReadOnlyList<ICommandHandler> _handlers = handlers.ToList();

    /// <summary>
    /// Finds a handler that can process the given message content.
    /// </summary>
    public ICommandHandler? FindHandler(string content)
    {
        return _handlers.FirstOrDefault(h => h.CanHandle(content));
    }

    /// <summary>
    /// Gets all registered command handlers.
    /// </summary>
    public IReadOnlyList<ICommandHandler> GetAllHandlers() => _handlers;
}
