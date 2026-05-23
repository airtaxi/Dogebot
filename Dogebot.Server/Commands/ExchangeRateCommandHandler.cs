using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class ExchangeRateCommandHandler(IExchangeRateService exchangeRateService, ILogger<ExchangeRateCommandHandler> logger) : ICommandHandler
{
    private const string ExchangeRateCommand = "!환율";

    public string Command => ExchangeRateCommand;

    public bool CanHandle(string content) => TryGetQueryText(content, out _);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            TryGetQueryText(data.Content, out var queryText);
            var message = await exchangeRateService.CreateExchangeRateMessageAsync(queryText);

            logger.LogInformation("[EXCHANGE_RATE] Exchange rate requested by {Sender} in room {RoomId}: {QueryText}", data.SenderName, data.RoomId, queryText);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[EXCHANGE_RATE] Error processing exchange rate command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "환율 조회 중 오류가 발생했습니다."
            };
        }
    }

    private static bool TryGetQueryText(string content, out string queryText)
    {
        queryText = string.Empty;
        var trimmedContent = content.Trim();
        if (trimmedContent.Equals(ExchangeRateCommand, StringComparison.OrdinalIgnoreCase)) return true;
        if (!trimmedContent.StartsWith(ExchangeRateCommand, StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmedContent.Length <= ExchangeRateCommand.Length || !char.IsWhiteSpace(trimmedContent[ExchangeRateCommand.Length])) return false;

        queryText = trimmedContent[ExchangeRateCommand.Length..].Trim();
        return true;
    }
}
