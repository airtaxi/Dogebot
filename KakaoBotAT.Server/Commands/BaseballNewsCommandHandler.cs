using System.Globalization;
using System.Text;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class BaseballNewsCommandHandler(
    IBaseballTeamRankingService baseballTeamRankingService,
    ILogger<BaseballNewsCommandHandler> logger) : ICommandHandler
{
    public string Command => "!야구뉴스";

    public bool CanHandle(string content) => content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var baseballNewsSnapshot = await baseballTeamRankingService.GetBaseballNewsSnapshotAsync();
            if (baseballNewsSnapshot == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = baseballTeamRankingService.GetLastNewsErrorDetails() ?? "야구 뉴스를 가져오지 못했습니다."
                };
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[BASEBALL_NEWS] Baseball news requested by {Sender} in room {RoomId}",
                    data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = FormatBaseballNewsMessage(baseballNewsSnapshot)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_NEWS] Error processing baseball news command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"Message: {exception.Message}\nStackTrace: {exception.StackTrace ?? "스택 추적 정보 없음"}"
            };
        }
    }

    private static string FormatBaseballNewsMessage(BaseballNewsSnapshot baseballNewsSnapshot)
    {
        var targetDateText = baseballNewsSnapshot.TargetDate.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        if (baseballNewsSnapshot.NewsItems.Count == 0)
            return $"⚾ KBO 뉴스 ({targetDateText} 기준)\n\n해당 날짜 야구 뉴스가 없습니다.";

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"⚾ KBO 뉴스 ({targetDateText} 기준)");
        stringBuilder.AppendLine();

        for (var newsItemIndex = 0; newsItemIndex < baseballNewsSnapshot.NewsItems.Count; newsItemIndex++)
        {
            var newsItem = baseballNewsSnapshot.NewsItems[newsItemIndex];
            stringBuilder.AppendLine($"{newsItemIndex + 1}. {newsItem.Title}");
            stringBuilder.AppendLine($"요약: {newsItem.Summary}");
            if (newsItemIndex < baseballNewsSnapshot.NewsItems.Count - 1) stringBuilder.AppendLine();
        }

        return stringBuilder.ToString().TrimEnd();
    }
}
