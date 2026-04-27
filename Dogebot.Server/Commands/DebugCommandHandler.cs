using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class DebugCommandHandler(
    IAdminService adminService,
    DebugLogService debugLogService) : ICommandHandler
{
    public string Command => "!디버그";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith(Command + " ", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        if (!await adminService.IsAdminAsync(data.SenderHash))
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "⛔ 권한이 없습니다."
            };
        }

        // Parse optional count: !디버그 [n]
        var trimmed = data.Content.Trim();
        var count = 10;

        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[1], out var parsed) && parsed > 0)
            count = Math.Min(parsed, 50);

        var entries = debugLogService.GetRecent(count);

        if (entries.Count == 0)
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "🐛 디버그 로그가 비어있습니다."
            };
        }

        var header = $"🐛 디버그 로그 (최근 {entries.Count}건 / 전체 {debugLogService.Count}건)\n\n";
        var logLines = string.Join("\n", entries.Select(e => e.ToString()));

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = header + logLines
        };
    }
}

