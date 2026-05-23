using System.Text;
using Dogebot.Commons;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the help command to display all available commands.
/// 
/// ⚠️ IMPORTANT: When adding a new command handler, you MUST update this file!
/// 
/// How to update:
/// 1. Find the category that fits your command.
/// 
/// 2. Add your command as a HelpEntry.
/// 
/// 3. If creating a new category, use emoji + category name format:
///    new("🆕 새 카테고리", ["새카테고리"], [new("!명령어", ["!명령어"], "설명")])
/// 
/// This ensures users can discover your new command through !도움말 or !help
/// </summary>
public class HelpCommandHandler(ILogger<HelpCommandHandler> logger) : ICommandHandler
{
    private sealed record HelpCategory(string Name, string[] Keywords, HelpEntry[] Entries);

    private sealed record HelpEntry(string DisplayCommand, string[] SearchCommands, string Description);

    private sealed record HelpSearchResult(HelpCategory Category, HelpEntry Entry);

    private static readonly string[] s_helpCommands = ["!도움말", "!도움", "!help"];

    private static readonly HelpCategory[] s_categories =
    [
        new("🎮 게임 & 랜덤", ["게임", "랜덤"], [
            new("!홀 / !짝", ["!홀", "!짝"], "홀짝 게임"),
            new("!주사위 (범위)", ["!주사위"], "1~범위 사이 랜덤 숫자 (최대: 2,147,483,647)"),
            new("확률", ["확률"], "0~100% 랜덤 확률"),
            new("!선택 (항목1) (항목2) ...", ["!선택"], "랜덤 선택"),
            new("!뭐먹지", ["!뭐먹지"], "음식 추천"),
            new("!코스요리", ["!코스요리"], "랜덤 코스요리 추천"),
            new("!차뽑기", ["!차뽑기"], "랜덤 차량 뽑기"),
            new("!행성뽑기", ["!행성뽑기"], "노 맨즈 스카이 랜덤 행성 생성"),
            new("!로또 [회차 수]", ["!로또"], "로또 번호 생성 (최대 10회)"),
            new("!운세", ["!운세"], "오늘의 운세 (재물운, 성공운, 애정운)")
        ]),
        new("🎭 재미", ["재미"], [
            new("판사님 (질문)", ["판사님"], "판결 내리기"),
            new("소라고동님 (질문)", ["소라고동님"], "마법의 소라고동님 소환"),
            new("댕", ["댕"], "멍멍 왈왈 으르르 컹컹"),
            new("!햄최몇", ["!햄최몇"], "한번에 먹을 수 있는 햄버거 개수")
        ]),
        new("💬 심심이", ["심심이"], [
            new("심심아 (메시지)", ["심심아"], "등록된 답변 조회"),
            new("!심등록 (메시지) / (답변)", ["!심등록"], "답변 등록 (개인톡 전용)"),
            new("!심몇개 (메시지)", ["!심몇개"], "답변 개수 확인"),
            new("!심랭킹 [개수]", ["!심랭킹"], "답변이 많은 메시지 TOP (최대 50개)")
        ]),
        new("📊 통계", ["통계"], [
            new("!랭킹 [인원수]", ["!랭킹"], "채팅 랭킹 TOP (최대 50명)"),
            new("!내랭킹", ["!내랭킹"], "내 순위 확인"),
            new("!랭크 [개수]", ["!랭크"], "많이 올라온 채팅 TOP (최대 50개)"),
            new("!단어랭크 [개수]", ["!단어랭크"], "많이 사용된 단어 TOP (최대 50개)"),
            new("!정보", ["!정보"], "방 정보 및 통계"),
            new("!시간통계", ["!시간통계"], "시간대별 채팅 통계"),
            new("!내시간통계", ["!내시간통계"], "내 시간대별 채팅 통계"),
            new("!요일통계", ["!요일통계"], "요일별 채팅 통계"),
            new("!내요일통계", ["!내요일통계"], "내 요일별 채팅 통계"),
            new("!월별통계", ["!월별통계"], "월별 채팅 통계"),
            new("!내월별통계", ["!내월별통계"], "내 월별 채팅 통계")
        ]),
        new("📈 주식", ["주식", "증시", "증권"], [new("!주식 [종목명/코드/티커]", ["!주식"], "현재가, 등락률, 장 상태 요약"), new("!주식상세 [종목명/코드/티커]", ["!주식상세"], "주요 지표, 기업 개요, 컨센서스 조회"), new("!주식차트 [종목명/코드/티커]", ["!주식차트"], "최근 일봉 가격/거래량 조회"), new("!주식뉴스 [종목명/코드/티커]", ["!주식뉴스"], "종목 뉴스 조회 (생략 시 증권 주요 뉴스)"), new("!증시 [국내/미국/나스닥/뉴욕/아멕스] [인기/시총/지수]", ["!증시"], "시장 지수와 종목 순위 조회"), new("!환율 / !환율 100달러 엔 / !환율 달러 엔 / !환율 미국 일본", ["!환율"], "환율 변환 (금액/달러 생략 가능)")]),
        new("👮 관리자", ["관리", "관리자", "관리[자]"], [
            new("!관리추가", ["!관리추가"], "관리자 승인 요청"),
            new("!관리추가 (코드)", ["!관리추가"], "관리자 승인 (최고 관리자 전용)"),
            new("!관리제거 (SenderHash)", ["!관리제거"], "관리자 제거 (최고 관리자 전용)"),
            new("!관리목록", ["!관리목록"], "등록된 관리자 목록 조회 (관리자 전용)"),
            new("!멀티메시지", ["!멀티메시지"], "pending 메시지 일괄 전송 활성화 (최고 관리자 전용)"),
            new("!싱글메시지", ["!싱글메시지"], "pending 메시지 단일 전송 활성화 (최고 관리자 전용)"),
            new("!제한설정 (횟수)", ["!제한설정"], "방의 1일 요청 제한 설정 (관리자 전용)"),
            new("!제한해제", ["!제한해제"], "방의 요청 제한 해제 (관리자 전용)"),
            new("!랭크활성화", ["!랭크활성화"], "메시지 내용 랭킹 활성화 (관리자 전용)"),
            new("!랭크비활성화", ["!랭크비활성화"], "메시지 내용 랭킹 비활성화 (관리자 전용)"),
            new("!심삭제 (메시지)", ["!심삭제"], "심심이 답변 삭제 (관리자 전용)"),
            new("!반복설정", ["!반복설정"], "반복 메시지 설정 (관리자 전용)"),
            new("!반복해제 (번호/전체)", ["!반복해제"], "반복 메시지 해제 (관리자 전용)"),
            new("!반복목록", ["!반복목록"], "반복 메시지 목록 조회 (관리자 전용)"),
            new("!용아맥설정 → !아이맥스설정", ["!용아맥설정", "!아이맥스설정"], "IMAX 알림 등록 (관리자 전용)"),
            new("!아이맥스해제", ["!아이맥스해제"], "IMAX 알림 해제 (관리자 전용)"),
            new("!아이맥스목록", ["!아이맥스목록"], "IMAX 알림 정보 조회 (관리자 전용)"),
            new("!방백업", ["!방백업"], "방 데이터 백업 코드 생성 (관리자 전용)"),
            new("!방복원 (코드)", ["!방복원"], "백업 데이터로 방 복원 (관리자 전용)")
        ]),
        new("⚾ 야구", ["야구"], [
            new("!야구팀순위 [팀명, 생략 시 전체 팀 성적 요약]", ["!야구팀순위"], "KBO 팀 순위 조회"),
            new("!야구타자순위", ["!야구타자순위"], "타율 TOP5, 홈런 TOP5 조회"),
            new("!야구투수순위", ["!야구투수순위"], "평균자책점 TOP5, 승리 TOP5 조회"),
            new("!야구관중순위", ["!야구관중순위"], "구단별 관중 수 순위 조회"),
            new("!야구뉴스", ["!야구뉴스"], "오늘 기준 KBO 뉴스 조회 (다음날 오전6시까지 오늘)"),
            new("!오늘야구 [팀명]", ["!오늘야구"], "오늘 KBO 경기 조회 (팀명 입력 시 상세 정보)"),
            new("!내일야구 [팀명]", ["!내일야구"], "내일 KBO 경기 조회 (팀명 입력 시 경기 전 상세 정보)"),
            new("!야구구독 [팀명]", ["!야구구독"], "경기 라인업/이벤트/점수 알림 구독"),
            new("!야구구독해제 [팀명]", ["!야구구독해제"], "야구 경기 알림 구독 해제")
        ]),
        new("ℹ️ 기타", ["기타"], [
            new("!날씨 [지역]", ["!날씨"], "현재 날씨 확인 (기본: 이전 도시 또는 서울)"),
            new("!내일날씨 [지역]", ["!내일날씨"], "내일 날씨 확인 (기본: 이전 도시 또는 서울)"),
            new("@Here", ["@Here", "Here"], "최근 10일간 활동한 사용자 호출 (일반 사용자 24시간 1회)"),
            new("@everyone", ["@everyone", "everyone"], "방의 알려진 전체 사용자 호출 (일반 사용자 24시간 1회)"),
            new("!핫딜", ["!핫딜"], "랜덤 핫딜 상품 추천"),
            new("!영화목록 [검색어]", ["!영화목록"], "CGV 영화 목록 조회 (영화관 선택)"),
            new("!아이맥스조회 [영화이름]", ["!아이맥스조회"], "IMAX 시간표 조회 (영화관 선택)"),
            new("!도움 / !도움말 / !help", ["!도움", "!도움말", "!help"], "이 메시지")
        ])
    ];

    public string Command => "!도움말";

    public bool CanHandle(string content) => TryGetHelpParameter(content, out _);

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parameter = GetHelpParameter(data.Content);
            var message = string.IsNullOrWhiteSpace(parameter)
                ? BuildFullHelpMessage()
                : BuildParameterizedHelpMessage(parameter);

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

    private static string GetHelpParameter(string content)
    {
        TryGetHelpParameter(content, out var parameter);
        return parameter;
    }

    private static bool TryGetHelpParameter(string content, out string parameter)
    {
        var trimmedContent = content.Trim();

        foreach (var helpCommand in s_helpCommands)
        {
            if (trimmedContent.Equals(helpCommand, StringComparison.OrdinalIgnoreCase))
            {
                parameter = string.Empty;
                return true;
            }

            if (trimmedContent.Length <= helpCommand.Length ||
                !trimmedContent.StartsWith(helpCommand, StringComparison.OrdinalIgnoreCase) ||
                !char.IsWhiteSpace(trimmedContent[helpCommand.Length]))
            {
                continue;
            }

            parameter = trimmedContent[helpCommand.Length..].Trim();
            return true;
        }

        parameter = string.Empty;
        return false;
    }

    private static string BuildParameterizedHelpMessage(string parameter)
    {
        var category = FindCategory(parameter);
        if (category is not null) return BuildCategorySection(category).TrimEnd('\n');

        var searchResults = FindSearchResults(parameter);
        if (searchResults.Count == 0) return BuildNotFoundMessage(parameter);

        return BuildSearchResultMessage(parameter, searchResults);
    }

    private static HelpCategory? FindCategory(string parameter)
    {
        var trimmedParameter = parameter.Trim();
        return s_categories.FirstOrDefault(category =>
            category.Keywords.Any(keyword =>
                keyword.Equals(trimmedParameter, StringComparison.OrdinalIgnoreCase)));
    }

    private static IReadOnlyList<HelpSearchResult> FindSearchResults(string parameter)
    {
        var normalizedParameter = NormalizeSearchText(parameter);
        if (normalizedParameter.Length == 0) return [];

        var exactMatches = FindSearchResults(normalizedParameter, useExactMatch: true);
        return exactMatches.Count > 0 ? exactMatches : FindSearchResults(normalizedParameter, useExactMatch: false);
    }

    private static List<HelpSearchResult> FindSearchResults(string normalizedParameter, bool useExactMatch)
    {
        var searchResults = new List<HelpSearchResult>();
        var seenDisplayCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in s_categories)
        {
            foreach (var entry in category.Entries)
            {
                if (!entry.SearchCommands.Any(command => IsCommandMatch(normalizedParameter, command, useExactMatch))) continue;

                if (!seenDisplayCommands.Add(entry.DisplayCommand)) continue;

                searchResults.Add(new HelpSearchResult(category, entry));
            }
        }

        return searchResults;
    }

    private static bool IsCommandMatch(string normalizedParameter, string command, bool useExactMatch)
    {
        var normalizedCommand = NormalizeSearchText(command);
        return useExactMatch
            ? normalizedCommand.Equals(normalizedParameter, StringComparison.OrdinalIgnoreCase)
            : normalizedCommand.Contains(normalizedParameter, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSearchText(string value)
    {
        var trimmedValue = value.Trim();
        return trimmedValue.StartsWith('!') ? trimmedValue[1..] : trimmedValue;
    }

    private static string BuildFullHelpMessage()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append("📖 사용 가능한 명령어\n\n");

        foreach (var category in s_categories)
        {
            stringBuilder.Append(BuildCategorySection(category));
            stringBuilder.Append('\n');
        }

        stringBuilder.Append("⚠️ 개인톡에서는 Android 한계로 답장이 안될 수 있습니다.\n");
        stringBuilder.Append("명령어 사용 전 \"댕\"으로 봇 작동 확인을 권장합니다.\n\n");
        stringBuilder.Append("━━━━━━━━━━━━━━━━━━\n");
        stringBuilder.Append("👨‍💻 제작자: 이호원\n");
        stringBuilder.Append("📦 소스코드:\n");
        stringBuilder.Append("https://github.com/airtaxi/Dogebot");

        return stringBuilder.ToString();
    }

    private static string BuildCategorySection(HelpCategory category)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(category.Name);
        stringBuilder.Append('\n');

        foreach (var entry in category.Entries)
        {
            stringBuilder.Append("• ");
            stringBuilder.Append(entry.DisplayCommand);
            stringBuilder.Append(" - ");
            stringBuilder.Append(entry.Description);
            stringBuilder.Append('\n');
        }

        return stringBuilder.ToString();
    }

    private static string BuildSearchResultMessage(string parameter, IReadOnlyList<HelpSearchResult> searchResults)
    {
        var stringBuilder = new StringBuilder();
        var previousCategoryName = string.Empty;

        stringBuilder.Append("🔎 도움말 검색 결과: ");
        stringBuilder.Append(parameter);
        stringBuilder.Append("\n\n");

        foreach (var searchResult in searchResults)
        {
            if (!searchResult.Category.Name.Equals(previousCategoryName, StringComparison.Ordinal))
            {
                if (previousCategoryName.Length > 0) stringBuilder.Append('\n');

                stringBuilder.Append(searchResult.Category.Name);
                stringBuilder.Append('\n');
                previousCategoryName = searchResult.Category.Name;
            }

            stringBuilder.Append("• ");
            stringBuilder.Append(searchResult.Entry.DisplayCommand);
            stringBuilder.Append(" - ");
            stringBuilder.Append(searchResult.Entry.Description);
            stringBuilder.Append('\n');
        }

        return stringBuilder.ToString().TrimEnd('\n');
    }

    private static string BuildNotFoundMessage(string parameter)
    {
        return $"'{parameter}'에 해당하는 도움말을 찾을 수 없습니다.\n" +
               "카테고리: 게임 / 재미 / 심심이 / 통계 / 주식 / 관리자 / 야구 / 기타";
    }
}

