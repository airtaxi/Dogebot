using Dogebot.Commons;

namespace Dogebot.Server.Commands;

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
public class HelpCommandHandler(ILogger<HelpCommandHandler> logger) : ICommandHandler
{
    public string Command => "!도움말";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               content.Trim().Equals("!도움", StringComparison.OrdinalIgnoreCase) ||
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
                         "• !선택 (항목1) (항목2) ... - 랜덤 선택\n" +
                         "• !뭐먹지 - 음식 추천\n" +
                         "• !코스요리 - 랜덤 코스요리 추천\n" +
                         "• !차뽑기 - 랜덤 차량 뽑기\n" +
                         "• !행성뽑기 - 노 맨즈 스카이 랜덤 행성 생성\n" +
                         "• !로또 [회차 수] - 로또 번호 생성 (최대 10회)\n" +
                         "• !운세 - 오늘의 운세 (재물운, 성공운, 애정운)\n\n" +
                         "🎭 재미\n" +
                         "• 판사님 (질문) - 판결 내리기\n" +
                         "• 소라고동님 (질문) - 마법의 소라고동님 소환\n" +
                         "• 댕 - 멍멍 왈왈 으르르 컹컹\n" +
                         "• !햄최몇 - 한번에 먹을 수 있는 햄버거 개수\n\n" +
                         "💬 심심이\n" +
                         "• 심심아 (메시지) - 등록된 답변 조회\n" +
                         "• !심등록 (메시지) / (답변) - 답변 등록 (개인톡 전용)\n" +
                         "• !심몇개 (메시지) - 답변 개수 확인\n" +
                         "• !심랭킹 [개수] - 답변이 많은 메시지 TOP (최대 50개)\n\n" +
                         "📊 통계\n" +
                         "• !랭킹 - 랭킹 조회 방법 안내\n" +
                         "• !조회 (roomId) - 채팅 랭킹 TOP 10\n" +
                         "• !내랭킹 - 내 순위 확인\n" +
                         "• !랭크 [개수] - 많이 올라온 채팅 TOP (최대 50개)\n" +
                         "• !단어랭크 [개수] - 많이 사용된 단어 TOP (최대 50개)\n" +
                         "• !정보 - 방 정보 및 통계\n" +
                         "• !시간통계 - 시간대별 채팅 통계\n" +
                         "• !내시간통계 - 내 시간대별 채팅 통계\n" +
                         "• !요일통계 - 요일별 채팅 통계\n" +
                         "• !내요일통계 - 내 요일별 채팅 통계\n" +
                         "• !월별통계 - 월별 채팅 통계\n" +
                         "• !내월별통계 - 내 월별 채팅 통계\n\n" +
                         "👮 관리자\n" +
                         "• !관리추가 - 관리자 승인 요청\n" +
                         "• !관리추가 (코드) - 관리자 승인 (최고 관리자 전용)\n" +
                         "• !관리제거 (SenderHash) - 관리자 제거 (최고 관리자 전용)\n" +
                         "• !관리목록 - 등록된 관리자 목록 조회 (관리자 전용)\n" +
                         "• !제한설정 (횟수) - 방의 1일 요청 제한 설정 (관리자 전용)\n" +
                         "• !제한해제 - 방의 요청 제한 해제 (관리자 전용)\n" +
                         "• !랭크활성화 - 메시지 내용 랭킹 활성화 (관리자 전용)\n" +
                         "• !랭크비활성화 - 메시지 내용 랭킹 비활성화 (관리자 전용)\n" +
                         "• !심삭제 (메시지) - 심심이 답변 삭제 (관리자 전용)\n" +
                         "• !반복설정 - 반복 메시지 설정 (관리자 전용)\n" +
                         "• !반복해제 (번호/전체) - 반복 메시지 해제 (관리자 전용)\n" +
                         "• !반복목록 - 반복 메시지 목록 조회 (관리자 전용)\n" +
                         "• !용아맥설정 → !아이맥스설정 - IMAX 알림 등록 (관리자 전용)\n" +
                         "• !아이맥스해제 - IMAX 알림 해제 (관리자 전용)\n" +
                         "• !아이맥스목록 - IMAX 알림 정보 조회 (관리자 전용)\n" +
                         "• !방백업 - 방 데이터 백업 코드 생성 (관리자 전용)\n" +
                         "• !방복원 (코드) - 백업 데이터로 방 복원 (관리자 전용)\n\n" +
                         "⚾ 야구\n" +
                         "• !야구팀순위 [팀명, 생략 시 전체 팀 성적 요약] - KBO 팀 순위 조회\n" +
                         "• !야구타자순위 - 타율 TOP5, 홈런 TOP5 조회\n" +
                         "• !야구투수순위 - 평균자책점 TOP5, 승리 TOP5 조회\n" +
                         "• !야구관중순위 - 구단별 관중 수 순위 조회\n" +
                         "• !야구뉴스 - 오늘 기준 KBO 뉴스 조회 (다음날 오전6시까지 오늘)\n" +
                         "• !오늘야구 [팀명] - 오늘 KBO 경기 조회 (팀명 입력 시 상세 정보)\n" +
                         "• !내일야구 [팀명] - 내일 KBO 경기 조회 (팀명 입력 시 경기 전 상세 정보)\n" +
                         "• !야구구독 [팀명] - 경기 라인업/이벤트/점수 알림 구독\n" +
                         "• !야구구독해제 [팀명] - 야구 경기 알림 구독 해제\n\n" +
                         "ℹ️ 기타\n" +
                         "• !날씨 [지역] - 현재 날씨 확인 (기본: 이전 도시 또는 서울)\n" +
                         "• !내일날씨 [지역] - 내일 날씨 확인 (기본: 이전 도시 또는 서울)\n" +
                         "• !핫딜 - 랜덤 핫딜 상품 추천\n" +
                         "• !영화목록 [검색어] - CGV 영화 목록 조회 (영화관 선택)\n" +
                         "• !아이맥스조회 [영화이름] - IMAX 시간표 조회 (영화관 선택)\n" +
                         "• !도움 / !도움말 / !help - 이 메시지\n\n" +
                         "⚠️ 개인톡에서는 Android 한계로 답장이 안될 수 있습니다.\n" +
                         "명령어 사용 전 \"댕\"으로 봇 작동 확인을 권장합니다.\n\n" +
                         "━━━━━━━━━━━━━━━━━━\n" +
                         "👨‍💻 제작자: 이호원\n" +
                         "📦 소스코드:\n" +
                         "https://github.com/airtaxi/Dogebot";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[HELP] Showing help message to {Sender} in room {RoomId}",
                    data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[HELP] Error processing help command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "도움말 표시 중 오류가 발생했습니다."
            });
        }
    }
}

