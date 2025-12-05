using KakaoBotAT.Server.Commands;
using KakaoBotAT.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Register HttpClient
builder.Services.AddHttpClient();

// Register MongoDB service
builder.Services.AddSingleton<IMongoDbService, MongoDbService>();

// Register statistics service
builder.Services.AddSingleton<IChatStatisticsService, ChatStatisticsService>();

// Register cleanup service
builder.Services.AddSingleton<MessageCleanupService>();

// Register SimSim service
builder.Services.AddSingleton<ISimSimService, SimSimService>();

// Register Weather service
builder.Services.AddSingleton<IWeatherService, WeatherService>();

// ⚠️ Register command handlers
// 
// IMPORTANT: When adding a new command handler, follow these 3 steps:
// 
// Step 1: Add the registration line here
//         builder.Services.AddSingleton<ICommandHandler, YourNewCommandHandler>();
// 
// Step 2: Update HelpCommandHandler.cs to include your command in the help message
//         Add your command under the appropriate category:
//         - 🎮 게임 & 랜덤 (Game & Random)
//         - 🎭 재미 (Fun)
//         - 📊 통계 (Statistics)
//         - ℹ️ 기타 (Others)
//         Format: "• [command] - [description]"
// 
// Step 3: Your command handler must implement ICommandHandler interface
//         See existing handlers for examples (DengCommandHandler, FoodRecommendCommandHandler, etc.)
// 
// Example:
// If you create "!날씨" command:
// 1. Add here: builder.Services.AddSingleton<ICommandHandler, WeatherCommandHandler>();
// 2. Update HelpCommandHandler.cs:
//    "ℹ️ 기타\n" +
//    "• !날씨 - 현재 날씨 확인\n" +
//    "• !도움말 / !help - 이 메시지"
//
builder.Services.AddSingleton<ICommandHandler, DengCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, RankingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, ViewRankingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, MyRankingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, RankCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, FoodRecommendCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, ProbabilityCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, JudgeCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, OddEvenCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, DiceCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, MagicConchCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, HelpCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, RoomInfoCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, CarGachaCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SimSimQueryCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SimSimRegisterCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SimSimDeleteCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SimSimCountCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, SimSimRankingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, WeatherCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, HamburgerCommandHandler>();
// Add more command handlers here as needed
// builder.Services.AddSingleton<ICommandHandler, YourNewCommandHandler>();

builder.Services.AddSingleton<CommandHandlerFactory>();
builder.Services.AddSingleton<IKakaoService, KakaoService>();
builder.Services.AddControllers();

var app = builder.Build();

// Run cleanup on startup to remove blacklisted messages from database
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var cleanupService = scope.ServiceProvider.GetRequiredService<MessageCleanupService>();
    
    try
    {
        logger.LogInformation("[STARTUP] Starting cleanup of blacklisted messages...");
        var deletedCount = await cleanupService.DeleteBlacklistedMessagesAsync();
        logger.LogInformation("[STARTUP] Cleanup completed. Deleted {Count} blacklisted messages.", deletedCount);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "[STARTUP] Error during cleanup of blacklisted messages");
    }
}

app.MapControllers();

app.Run();