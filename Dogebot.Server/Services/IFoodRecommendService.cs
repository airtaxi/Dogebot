namespace Dogebot.Server.Services;

public interface IFoodRecommendService : IDengAiCallableService
{
    string RecommendFood();
}
