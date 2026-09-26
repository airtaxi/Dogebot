using Dogebot.LocoClient.Configuration;
using Dogebot.LocoClient.Services;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// Configuration comes from environment variables only.
builder.Services.AddSingleton<IOptions<LocoClientOptions>>(Options.Create(LocoClientOptionsEnvironmentLoader.Load()));

builder.Services.AddHttpClient<IDogebotServerApiClient, DogebotServerApiClient>();
builder.Services.AddSingleton<ILocoCliApiClient, LocoCliApiClient>();
builder.Services.AddHostedService<LocoBridgeService>();

await builder.Build().RunAsync();
