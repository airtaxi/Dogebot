using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class IdentityLinkCommandHandler(IUserIdentityLinkService userIdentityLinkService, IAdminService adminService, ILogger<IdentityLinkCommandHandler> logger) : ICommandHandler
{
    private const int DisplayHashLength = 16;

    public string Command => "!신원연결";

    public bool CanHandle(string content)
    {
        var trimmedContent = content.Trim();
        return trimmedContent.Equals(Command, StringComparison.OrdinalIgnoreCase) || trimmedContent.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (!await adminService.IsAdminAsync(data.SenderHash)) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "⛔ 권한이 없습니다. 관리자만 신원을 연결할 수 있습니다." };

            var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) return CreateUsageResponse(data);

            var sourceResolution = await userIdentityLinkService.ResolveIdentityAsync(data.RoomId, parts[1]);
            if (!sourceResolution.IsResolved) return CreateResolutionFailureResponse(data, "소스", parts[1], sourceResolution);

            var targetResolution = await userIdentityLinkService.ResolveIdentityAsync(data.RoomId, parts[2]);
            if (!targetResolution.IsResolved) return CreateResolutionFailureResponse(data, "타겟", parts[2], targetResolution);

            var source = sourceResolution.Identity!;
            var target = targetResolution.Identity!;

            if (source.SenderHash == target.SenderHash) return CreateSameIdentityResponse(data, source);

            await userIdentityLinkService.LinkIdentitiesAsync(data.RoomId, source.SenderHash, target.SenderHash);

            if (logger.IsEnabled(LogLevel.Warning)) logger.LogWarning("[IDENTITY_LINK] {Sender} linked identities in room {RoomName}. source={SourceName}({SourceHash}), target={TargetName}({TargetHash})", data.SenderName, data.RoomName, source.SenderName, source.SenderHash, target.SenderName, target.SenderHash);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 신원 연결 완료!\n\n" +
                          $"📎 병합: {source.SenderName} ({FormatHash(source.SenderHash)}) → {target.SenderName} ({FormatHash(target.SenderHash)})\n" +
                          $"📊 병합된 메시지: {source.MessageCount:N0}건\n\n" +
                          $"ℹ️ 연결은 현재 방에만 적용됩니다."
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[IDENTITY_LINK] Error processing identity link command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "신원 연결 중 오류가 발생했습니다."
            };
        }
    }

    private static ServerResponse CreateUsageResponse(KakaoMessageData data) => new()
    {
        Action = "send_text",
        RoomId = data.RoomId,
        Message = "⚙️ 사용법:\n\n" + "!신원연결 (소스) (타겟)\n\n" + "• 소스: 타겟으로 합쳐질 신원\n" + "• 타겟: 기록이 남을 신원\n" + "• 해시 또는 닉네임으로 지정합니다. (해시 우선 검사)\n" + "• 닉네임에 공백이 있으면 해시로 지정해주세요.\n\n" + "예: !신원연결 홍길동 홍길동2"
    };

    private static ServerResponse CreateResolutionFailureResponse(KakaoMessageData data, string label, string identityArgument, SenderIdentityResolution resolution)
    {
        if (resolution.IsAmbiguous)
        {
            var candidateLines = string.Join("\n", resolution.AmbiguousCandidates.Select(candidate => $"• {candidate.SenderName} ({candidate.SenderHash}) - 메시지 {candidate.MessageCount:N0}건, 최근 활동 {FormatLastActivity(candidate.LastMessageTime)}"));

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"❌ {label} '{identityArgument}'에 해당하는 신원이 여러 개입니다.\n\n" +
                          $"{candidateLines}\n\n" +
                          $"해시를 직접 지정해 다시 시도해주세요."
            };
        }

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = $"❌ {label} '{identityArgument}' 신원을 찾을 수 없습니다.\n\n" +
                      $"이 방에서 활동한 적 있는 해시 또는 닉네임만 지정할 수 있습니다."
        };
    }

    private static ServerResponse CreateSameIdentityResponse(KakaoMessageData data, SenderIdentityReference identity) => new()
    {
        Action = "send_text",
        RoomId = data.RoomId,
        Message = $"❌ 소스와 타겟이 동일한 신원입니다.\n\n" + $"📎 {identity.SenderName} ({FormatHash(identity.SenderHash)})"};

    private static string FormatHash(string senderHash) => senderHash.Length <= DisplayHashLength ? senderHash : $"{senderHash[..DisplayHashLength]}...";

    private static string FormatLastActivity(long lastMessageTimeMilliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(lastMessageTimeMilliseconds).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}
