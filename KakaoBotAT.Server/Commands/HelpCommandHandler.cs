using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the help command to display all available commands.
/// 
/// ⚠️ IMPORTANT: When adding a new command handler, you MUST update this file!
/// 
/// How to update:
/// 1. Find the category that fits your command:
///    - 🎮 게임 & 랜덤 (Game & Random) - for games and random features
///    - 🎭 재미 (Fun) - for entertainment commands
///    - 📊 통계 (Statistics) - for statistics and ranking commands
///    - ℹ️ 기타 (Others) - for utility and miscellaneous commands
/// 
/// 2. Add your command in the format: "• [command] - [description]\n"
///    Examples:
///    - "• !날씨 - 현재 날씨 확인\n"
///    - "• !번역 (텍스트) - 영어로 번역\n"
///    - "• 안녕 - 인사하기\n"
/// 
/// 3. If creating a new category, use emoji + category name format:
///    "🆕 새 카테고리\n" +
///    "• !명령어 - 설명\n\n"
/// 
/// This ensures users can discover your new command through !도움말 or !help
/// </summary>
public class HelpCommandHandler : ICommandHandler
{
    private readonly ILogger<HelpCommandHandler> _logger;

    public HelpCommandHandler(ILogger<HelpCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "!도움말";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               content.Trim().Equals("!help", StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            // ⚠️ ADD YOUR NEW COMMAND HERE! Update the appropriate category below.
            var message = "📖 사용 가능한 명령어\n\n" +
                         "🎮 게임 & 랜덤\n" +
                         "• !홀 / !짝 - 홀짝 게임\n" +
                         "• !주사위 (범위) - 1~범위 사이 랜덤 숫자 (최대: 2,147,483,647)\n" +
                         "• 확률 - 0~100% 랜덤 확률\n" +
                         "• !뭐먹지 - 음식 추천\n\n" +
                         "🎭 재미\n" +
                         "• 판사님 (질문) - 판결 내리기\n" +
                         "• 소라고동님 (질문) - 매직 소라고동\n" +
                         "• 댕 - 댕\n\n" +
                         "📊 통계\n" +
                         "• !랭킹 - 랭킹 조회 방법 안내\n" +
                         "• !조회 (roomId) - 채팅 랭킹 TOP 10\n" +
                         "• !내랭킹 - 내 순위 확인\n" +
                         "• !랭크 [개수] - 많이 올라온 채팅 TOP (최대 50개)\n" +
                         "• !정보 - 방 정보 및 통계\n\n" +
                         "ℹ️ 기타\n" +
                         "• !도움말 / !help - 이 메시지";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[HELP] Showing help message to {Sender} in room {RoomId}", 
                    data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HELP] Error processing help command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "도움말 표시 중 오류가 발생했습니다."
            });
        }
    }
}
