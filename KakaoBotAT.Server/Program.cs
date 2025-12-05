using KakaoBotAT.Server.Commands;
using KakaoBotAT.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Register command handlers
builder.Services.AddSingleton<ICommandHandler, PingCommandHandler>();
// Add more command handlers here as needed
// builder.Services.AddSingleton<ICommandHandler, YourNewCommandHandler>();

builder.Services.AddSingleton<CommandHandlerFactory>();
builder.Services.AddSingleton<IKakaoService, KakaoService>();
builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();