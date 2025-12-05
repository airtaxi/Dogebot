using KakaoBotAT.Server.Commands;
using KakaoBotAT.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Register MongoDB service
builder.Services.AddSingleton<IMongoDbService, MongoDbService>();

// Register statistics service
builder.Services.AddSingleton<IChatStatisticsService, ChatStatisticsService>();

// Register command handlers
builder.Services.AddSingleton<ICommandHandler, PingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, RankingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, MyRankingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, RankCommandHandler>();
// Add more command handlers here as needed
// builder.Services.AddSingleton<ICommandHandler, YourNewCommandHandler>();

builder.Services.AddSingleton<CommandHandlerFactory>();
builder.Services.AddSingleton<IKakaoService, KakaoService>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();