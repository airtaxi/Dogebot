using Dogebot.DiscordClient.Adapters;
using Dogebot.DiscordClient.Configuration;
using Dogebot.DiscordClient.Contracts;
using Dogebot.DiscordClient.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<DiscordClientOptions>(builder.Configuration.GetSection(DiscordClientOptions.SectionName));

builder.Services.AddHttpClient<IServerApiClient, ServerApiClient>();
builder.Services.AddSingleton<IDiscordMessageMapper, DiscordMessageMapper>();
builder.Services.AddSingleton<IDiscordGatewayClient, DiscordNetGatewayClient>();
builder.Services.AddSingleton<IDiscordResponseExecutor, DiscordResponseExecutor>();

builder.Services.AddHostedService<DiscordGatewayHostedService>();
builder.Services.AddHostedService<DiscordNotificationService>();
builder.Services.AddHostedService<PollingService>();

await builder.Build().RunAsync();

