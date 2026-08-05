using System.Text;
using Dogebot.Commons;
using Dogebot.Server.Models;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class NewsCommandHandler(WebSearchService webSearchService, ILogger<NewsCommandHandler> logger) : ICommandHandler
{
    private const int MaximumDisplayResultCount = 10;
    private const int MaximumFieldCharacterCount = 127;

    public string Command => "!뉴스";

    public bool CanHandle(string content) => content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var content = data.Content.Trim();
            var query = content.Length > Command.Length ? content[Command.Length..].Trim() : string.Empty;

            if (string.IsNullOrWhiteSpace(query)) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "📰 사용법: !뉴스 [검색어]\n예시: !뉴스 하이닉스" };

            var results = await webSearchService.SearchAsync(query, MaximumDisplayResultCount, WebSearchService.NormalizeTopic("news"), CancellationToken.None);
            if (results is null || results.Count == 0) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = $"📰 '{query}' 뉴스 검색 결과가 없습니다." };

            var message = BuildResultMessage(query, results.Take(MaximumDisplayResultCount));

            if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation("[NEWS] News search performed by {Sender} in room {RoomId} for query: {Query}, result count: {Count}", data.SenderName, data.RoomId, query, results.Count);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[NEWS] Error processing news command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "뉴스 검색 중 오류가 발생했습니다."
            };
        }
    }

    private static string BuildResultMessage(string query, IEnumerable<WebSearchResult> results)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append($"📰 '{query}' 뉴스 결과\n\n");

        foreach (var result in results)
        {
            var title = TruncateField(NormalizeNewlines(result.Title ?? "(제목 없음)"));
            var content = TruncateField(NormalizeNewlines(result.Content ?? string.Empty));

            stringBuilder.Append($"📌 {title}\n");
            if (!string.IsNullOrWhiteSpace(content)) stringBuilder.Append($"{content}\n");
            if (!string.IsNullOrWhiteSpace(result.Url)) stringBuilder.Append($"🔗 {result.Url}\n");
            stringBuilder.Append('\n');
        }

        return stringBuilder.ToString().TrimEnd();
    }

    private static string NormalizeNewlines(string value) => value.ReplaceLineEndings(" ");

    private static string TruncateField(string value)
    {
        if (value.Length <= MaximumFieldCharacterCount) return value;
        return string.Concat(value.AsSpan(0, MaximumFieldCharacterCount).TrimEnd(), "…");
    }
}
