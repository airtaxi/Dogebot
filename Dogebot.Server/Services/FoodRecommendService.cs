using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public class FoodRecommendService : IFoodRecommendService
{
    private static readonly string[] s_foods =
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

    private readonly Random _random = new();

    public string RecommendFood() => s_foods[_random.Next(s_foods.Length)];

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("recommend_food", "Recommend a random Korean food menu for the user who cannot decide what to eat.", DengAiJsonSchema.Object())
    ];

    Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("recommend_food", StringComparison.Ordinal)) return Task.FromResult("Unknown food recommendation tool.");
        return Task.FromResult(RecommendFood());
    }

    #endregion
}
