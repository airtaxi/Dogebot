using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !코스요리 command to create a random course meal.
/// Selects appetizer, main dish, and dessert from a fantasy-themed menu.
/// </summary>
public class CourseMealCommandHandler : ICommandHandler
{
    private readonly ILogger<CourseMealCommandHandler> _logger;
    private readonly Random _random = new();

    private static readonly string[] Dishes =
    [
        "수렁늪 거인 포자 튀김",
        "검은바위 용암게 내장찜",
        "잿빛골짜기 밤표범 허리살 구이",
        "실바나스의 눈물 절임 샐러드",
        "황천비룡 꼬리 수프",
        "저주받은 해골버섯 크림리조또",
        "천둥절벽 들소심장 직화구이",
        "붉은십자군 성수 마리네이드 치킨",
        "역병지대 썩은호박 잼 토스트",
        "무쇠드워프 맥주효모 빵",
        "아제로스 심연조개 버터구이",
        "불타는 군단 지옥고추 파스타",
        "낙스라마스 거미알 오믈렛",
        "서리늑대 부족 훈제 늑대갈비",
        "타나리스 모래가재 그라탕",
        "유령의 뼈마루 골수 스튜",
        "어둠달 골짜기 광기초 초무침",
        "검은심연 나가 해초튀김",
        "붉은평원 핏빛사슴 타르타르",
        "하이잘 세계수 수액 캐러멜",
        "스톰윈드 하수구쥐 라구소스",
        "오그리마 전투멧돼지 족발찜",
        "잊혀진 왕의 왕관빵(철관빵)",
        "광기의 촉수볶음(살짝 미디움)",
        "어비스의 심장 껍질찜",
        "바람추적자 번개새우 꼬치",
        "티리스팔 망령양파 수프",
        "울부짖는 협만 바다이끼 냉채",
        "빛의 성채 성기사 소금절이 대구",
        "고대정령 나무껍질 칩",
        "지하왕국 굴착벌레 등심 스테이크",
        "붉은용군단 화염비늘 구이",
        "청동용군단 시간숙성 치즈",
        "공허방랑자 먹물 라멘",
        "무너진 사원의 저주비단 두부찜",
        "은빛소나무 숲 독안개 베리 파이",
        "설원 맘모스 기름 감자볶음",
        "크라켄 촉수 간장버터 구이",
        "폭풍해안 소금폭탄 조개탕",
        "사령관의 피묻은 전투식량 볶음밥"
    ];

    public CourseMealCommandHandler(ILogger<CourseMealCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "!코스요리";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            // Select 3 unique random dishes for the course
            var selectedIndices = new HashSet<int>();
            while (selectedIndices.Count < 3)
            {
                selectedIndices.Add(_random.Next(Dishes.Length));
            }

            var dishes = selectedIndices.Select(i => Dishes[i]).ToArray();

            var message = "🍽️ 오늘의 코스요리\n\n" +
                         $"🥗 전채: {dishes[0]}\n\n" +
                         $"🍖 메인: {dishes[1]}\n\n" +
                         $"🍰 디저트: {dishes[2]}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[COURSE_MEAL] Generated course meal for {Sender} in room {RoomId}: [{Appetizer}], [{Main}], [{Dessert}]",
                    data.SenderName, data.RoomId, dishes[0], dishes[1], dishes[2]);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[COURSE_MEAL] Error processing course meal command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "코스요리 구성 중 오류가 발생했습니다."
            });
        }
    }
}
