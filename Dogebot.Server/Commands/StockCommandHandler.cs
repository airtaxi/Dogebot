using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class StockCommandHandler(
    IStockService stockService,
    ILogger<StockCommandHandler> logger) : ICommandHandler
{
    private const string SummaryCommand = "!주식";
    private const string DetailCommand = "!주식상세";
    private const string ChartCommand = "!주식차트";
    private const string NewsCommand = "!주식뉴스";
    private const string MarketCommand = "!증시";

    public string Command => SummaryCommand;

    public bool CanHandle(string content) => TryCreateCommandContext(content.Trim()) != null;

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var commandContext = TryCreateCommandContext(data.Content.Trim());
            if (commandContext == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "주식 명령어를 처리할 수 없습니다."
                };
            }

            var message = commandContext.CommandType switch
            {
                StockCommandType.Detail => await stockService.CreateDetailMessageAsync(commandContext.QueryText),
                StockCommandType.Chart => await stockService.CreateChartMessageAsync(commandContext.QueryText),
                StockCommandType.News => await stockService.CreateNewsMessageAsync(commandContext.QueryText),
                StockCommandType.Market => await stockService.CreateMarketMessageAsync(commandContext.QueryText),
                _ => await stockService.CreateSummaryMessageAsync(commandContext.QueryText)
            };

            logger.LogInformation("[STOCK] Stock command {Command} requested by {Sender} in room {RoomId}",
                commandContext.Command, data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[STOCK] Error processing stock command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "주식 정보를 가져오지 못했습니다.\n잠시 후 다시 시도해주세요."
            };
        }
    }

    private static StockCommandContext? TryCreateCommandContext(string content)
    {
        if (TryMatchCommand(content, DetailCommand, out var detailQueryText)) return new StockCommandContext(DetailCommand, StockCommandType.Detail, detailQueryText);
        if (TryMatchCommand(content, ChartCommand, out var chartQueryText)) return new StockCommandContext(ChartCommand, StockCommandType.Chart, chartQueryText);
        if (TryMatchCommand(content, NewsCommand, out var newsQueryText)) return new StockCommandContext(NewsCommand, StockCommandType.News, newsQueryText);
        if (TryMatchCommand(content, MarketCommand, out var marketQueryText)) return new StockCommandContext(MarketCommand, StockCommandType.Market, marketQueryText);
        if (TryMatchCommand(content, SummaryCommand, out var summaryQueryText)) return new StockCommandContext(SummaryCommand, StockCommandType.Summary, summaryQueryText);

        return null;
    }

    private static bool TryMatchCommand(string content, string command, out string queryText)
    {
        queryText = string.Empty;
        if (content.Equals(command, StringComparison.OrdinalIgnoreCase)) return true;
        if (!content.StartsWith(command, StringComparison.OrdinalIgnoreCase)) return false;
        if (content.Length <= command.Length || !char.IsWhiteSpace(content[command.Length])) return false;

        queryText = content[command.Length..].Trim();
        return true;
    }

    private sealed record StockCommandContext(string Command, StockCommandType CommandType, string QueryText);

    private enum StockCommandType
    {
        Summary,
        Detail,
        Chart,
        News,
        Market
    }
}
