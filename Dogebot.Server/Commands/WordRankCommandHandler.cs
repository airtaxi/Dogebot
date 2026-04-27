using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class WordRankCommandHandler(
    IChatStatisticsService statisticsService,
    ILogger<WordRankCommandHandler> logger) : ICommandHandler
{
    public string Command => "!단어랭크";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (!await statisticsService.IsMessageContentEnabledAsync(data.RoomId))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 이 방은 랭킹이 비활성화되어 있습니다.\n\n" +
                             "관리자가 !랭크활성화 명령어로 활성화할 수 있습니다."
                };
            }

            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var limit = 10;

            if (parts.Length > 1 && int.TryParse(parts[1], out var parsedLimit))
            {
                limit = Math.Max(1, Math.Min(parsedLimit, 50));
            }

            var topWords = await statisticsService.GetTopWordsAsync(data.RoomId, limit);

            if (topWords.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "아직 통계 데이터가 없습니다."
                };
            }

            var message = $"📝 많이 사용된 단어 TOP {limit}\n\n";
            for (int i = 0; i < topWords.Count; i++)
            {
                var (word, count) = topWords[i];
                var medal = i switch
                {
                    0 => "🥇",
                    1 => "🥈",
                    2 => "🥉",
                    _ => $"{i + 1}."
                };

                var displayWord = word switch
                {
                    [var c, var c2, var c3] when c == c2 && c2 == c3 && c is >= 'ㄱ' and <= 'ㅎ'
                        => $"{c}, {c}{c} 등",
                    _ when word.Length > 30 => word[..27] + "...",
                    _ => word
                };
                message += $"{medal} {displayWord} ({count:N0}회)\n";
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[WORD_RANK] Showing top {Limit} words for room {RoomId}", limit, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message.TrimEnd()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[WORD_RANK] Error processing word rank command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "단어 랭크 조회 중 오류가 발생했습니다."
            };
        }
    }
}

