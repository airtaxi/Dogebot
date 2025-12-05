using KakaoBotAT.Commons;
using KakaoBotAT.Server.Commands;
using System.Diagnostics;

namespace KakaoBotAT.Server.Services;

/// <summary>
/// Service implementation that handles bot logic.
/// </summary>
public class KakaoService : IKakaoService
{
    private readonly ILogger<KakaoService> _logger;
    private readonly CommandHandlerFactory _commandHandlerFactory;
    private readonly IChatStatisticsService _chatStatisticsService;

    public KakaoService(
        ILogger<KakaoService> logger, 
        CommandHandlerFactory commandHandlerFactory,
        IChatStatisticsService chatStatisticsService)
    {
        _logger = logger;
        _commandHandlerFactory = commandHandlerFactory;
        _chatStatisticsService = chatStatisticsService;
    }

    /// <summary>
    /// Processes received notifications and executes appropriate command handlers.
    /// </summary>
    public async Task<ServerResponse> HandleNotificationAsync(ServerNotification notification)
    {
        var data = notification.Data;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[NOTIFY] Received from Room: {RoomName}, Sender: {SenderName}, Content: {Content}", 
                data.RoomName, data.SenderName, data.Content);

        // Record message statistics
        await _chatStatisticsService.RecordMessageAsync(data);

        // Find and execute appropriate command handler
        var handler = _commandHandlerFactory.FindHandler(data.Content);
        if (handler != null)
        {
            return await handler.HandleAsync(data);
        }

        return new ServerResponse();
    }

    /// <summary>
    /// Retrieves queued commands. (Currently the queued command feature is not implemented.)
    /// </summary>
    public Task<ServerResponse> GetPendingCommandAsync()
    {
        // Currently there is no queued command feature, so return an empty response (to allow the client to process without errors)
        return Task.FromResult(new ServerResponse());
    }
}