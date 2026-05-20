using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class RoomMentionCommandHandler(IChatStatisticsService chatStatisticsService, IRoomMentionUsageService roomMentionUsageService, IAdminService adminService, ILogger<RoomMentionCommandHandler> logger) : ICommandHandler
{
    private const int HereMentionLookbackDays = 10;

    private static readonly Regex s_mentionCommandRegularExpression = new(@"(?<![\p{L}\p{Nd}_@.])@(here|everyone)(?![\p{L}\p{Nd}_.])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly TimeSpan s_koreaStandardTimeOffset = TimeSpan.FromHours(9);

    public string Command => "@Here";

    public bool CanHandle(string content) => TryGetMentionCommand(content, out _);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (!string.Equals(data.Source, KakaoMessageData.KakaoSource, StringComparison.OrdinalIgnoreCase)) return new ServerResponse();

            if (!TryGetMentionCommand(data.Content, out var mentionCommand)) return new ServerResponse();

            var minimumLastMessageTimeMilliseconds = mentionCommand == "@Here"
                ? DateTimeOffset.UtcNow.AddDays(-HereMentionLookbackDays).ToUnixTimeMilliseconds()
                : (long?)null;
            var knownSenderNames = await chatStatisticsService.GetKnownSenderNamesAsync(data.RoomId, data.SenderHash, minimumLastMessageTimeMilliseconds);
            if (knownSenderNames.Count == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "호출할 수 있는 알려진 사용자가 없습니다."
                };
            }

            var isAdmin = await adminService.IsAdminAsync(data.SenderHash);
            long? nextAvailableAt = null;

            if (!isAdmin)
            {
                var currentUnixTimeSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var usageResult = await roomMentionUsageService.TryUseAsync(data.RoomId, data.RoomName, data.SenderHash, data.SenderName, currentUnixTimeSeconds);

                if (!usageResult.CanUse)
                {
                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = "아직 @Here/@everyone을 사용할 수 없습니다.\n" +
                                  $"다음 호출 가능 시간: {FormatKoreaStandardTime(usageResult.NextAvailableAt)}"
                    };
                }

                nextAvailableAt = usageResult.NextAvailableAt;
            }

            logger.LogInformation("[ROOM_MENTION] {Sender} used {MentionCommand} in room {RoomName}", data.SenderName, mentionCommand, data.RoomName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = BuildMentionMessage(data.SenderName, mentionCommand, knownSenderNames, nextAvailableAt)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[ROOM_MENTION] Error processing room mention command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "호출 처리 중 오류가 발생했습니다."
            };
        }
    }

    private static bool TryGetMentionCommand(string content, out string mentionCommand)
    {
        var match = s_mentionCommandRegularExpression.Match(content);
        if (!match.Success)
        {
            mentionCommand = string.Empty;
            return false;
        }

        mentionCommand = match.Groups[1].Value.Equals("everyone", StringComparison.OrdinalIgnoreCase)
            ? "@everyone"
            : "@Here";
        return true;
    }

    private static string BuildMentionMessage(string senderName, string mentionCommand, IReadOnlyList<string> knownSenderNames, long? nextAvailableAt)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(senderName);
        stringBuilder.Append(" 이 ");
        stringBuilder.Append(mentionCommand);
        stringBuilder.AppendLine(" 을 사용하였습니다:");

        foreach (var knownSenderName in knownSenderNames)
        {
            stringBuilder.Append('@');
            stringBuilder.AppendLine(knownSenderName);
        }

        if (nextAvailableAt.HasValue)
        {
            stringBuilder.AppendLine();
            stringBuilder.Append("다음 호출 가능 시간: ");
            stringBuilder.Append(FormatKoreaStandardTime(nextAvailableAt.Value));
        }

        return stringBuilder.ToString();
    }

    private static string FormatKoreaStandardTime(long unixTimeSeconds)
    {
        var dateTime = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds).ToOffset(s_koreaStandardTimeOffset);
        return dateTime.ToString("yyyy-MM-dd HH:mm:ss 'KST'", CultureInfo.InvariantCulture);
    }
}
