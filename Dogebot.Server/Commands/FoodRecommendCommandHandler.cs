using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class FoodRecommendCommandHandler(ILogger<FoodRecommendCommandHandler> logger, IFoodRecommendService foodRecommendService) : ICommandHandler
{
    public string Command => "!뭐먹지";

    public bool CanHandle(string content)
    {
        var legitCommand = content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
        var hiddenCommand = content.Trim().Equals("!대댁거", StringComparison.OrdinalIgnoreCase);
        return legitCommand || hiddenCommand;
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var recommendedFood = foodRecommendService.RecommendFood();
            var message = $"🍴 오늘의 추천 메뉴: {recommendedFood}";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[FOOD] Recommended '{Food}' to {Sender} in room {RoomId}", recommendedFood, data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FOOD] Error processing food recommendation command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "음식 추천 중 오류가 발생했습니다."
            });
        }
    }
}

