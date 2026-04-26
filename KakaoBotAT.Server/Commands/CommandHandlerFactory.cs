namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Factory for managing and retrieving command handlers.
/// 
/// ⚠️ IMPORTANT: This factory automatically receives all registered ICommandHandler implementations.
/// All command handlers registered in Program.cs will be available here through dependency injection.
/// 
/// When adding a new command:
/// 1. Create a new class implementing ICommandHandler
/// 2. Register it in Program.cs as: builder.Services.AddSingleton&lt;ICommandHandler, YourCommandHandler&gt;();
/// 3. Update HelpCommandHandler.cs to include the command in the help message
/// 
/// No changes needed in this file - it automatically picks up all registered handlers!
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
