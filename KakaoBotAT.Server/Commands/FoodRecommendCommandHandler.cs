using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

public class FoodRecommendCommandHandler : ICommandHandler
{
    private readonly ILogger<FoodRecommendCommandHandler> _logger;
    private readonly Random _random = new();

    private static readonly string[] Foods =
    [
        "김치찌개", "된장찌개", "순두부찌개", "부대찌개", "제육볶음",
        "삼겹살", "목살", "치킨", "피자", "햄버거",
        "짜장면", "짬뽕", "탕수육", "볶음밥", "우동",
        "라면", "떡볶이", "김밥", "라볶이", "쫄면",
        "냉면", "비빔밥", "김치볶음밥", "돈까스", "돈부리",
        "초밥", "회", "해물탕", "아구찜", "갈비찜",
        "삼계탕", "설렁탕", "곰탕", "감자탕", "해장국",
        "칼국수", "수제비", "국밥", "순대국", "뼈해장국",
        "족발", "보쌈", "양념치킨", "간장치킨", "후라이드치킨",
        "파스타", "스테이크", "샐러드", "샌드위치"
    ];

    public FoodRecommendCommandHandler(ILogger<FoodRecommendCommandHandler> logger)
    {
        _logger = logger;
    }

    public string Command => "!뭐먹지";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var recommendedFood = Foods[_random.Next(Foods.Length)];
            var message = $"🍴 오늘의 추천 메뉴: {recommendedFood}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[FOOD] Recommended '{Food}' to {Sender} in room {RoomId}", 
                    recommendedFood, data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FOOD] Error processing food recommendation command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "음식 추천 중 오류가 발생했습니다."
            });
        }
    }
}
