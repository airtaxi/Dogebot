using Discord;
using Discord.WebSocket;
using KakaoBotAT.DiscordClient.Configuration;
using KakaoBotAT.DiscordClient.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace KakaoBotAT.DiscordClient.Adapters;

public class DiscordNetGatewayClient(
    ILogger<DiscordNetGatewayClient> logger,
    IOptions<DiscordClientOptions> options) : IDiscordGatewayClient
{
    private readonly DiscordSocketClient _client = new(new DiscordSocketConfig
    {
        GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
        LogLevel = LogSeverity.Info
    });

    private readonly ConcurrentDictionary<string, byte> _activeRoomIds = [];

    public event Func<DiscordInboundMessage, Task>? MessageReceived;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var token = options.Value.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("[DISCORD] Token is empty. Gateway start skipped.");
            return;
        }

        _client.Log += OnDiscordLogAsync;
        _client.MessageReceived += OnMessageReceivedAsync;

        await _client.LoginAsync(TokenType.Bot, token);
        await _client.StartAsync();

        logger.LogInformation("[DISCORD] Gateway started.");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _client.MessageReceived -= OnMessageReceivedAsync;
        _client.Log -= OnDiscordLogAsync;

        await _client.StopAsync();
        await _client.LogoutAsync();

        logger.LogInformation("[DISCORD] Gateway stopped.");
    }

    public async Task SendMessageAsync(string channelId, string message, CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(channelId, out var discordChannelId))
        {
            logger.LogWarning("[DISCORD] Invalid channel id: {ChannelId}", channelId);
            return;
        }

        var channel = _client.GetChannel(discordChannelId) as IMessageChannel;
        if (channel == null)
        {
            logger.LogWarning("[DISCORD] Channel not found: {ChannelId}", channelId);
            return;
        }

        await channel.SendMessageAsync(message);
    }

    public IReadOnlyList<string> GetAvailableRoomIds()
    {
        var allowList = options.Value.AllowedChannelIds;
        if (allowList.Count > 0)
            return allowList;

        return [.. _activeRoomIds.Keys];
    }

    private Task OnDiscordLogAsync(LogMessage message)
    {
        logger.LogInformation("[DISCORD] {Severity}: {Message}", message.Severity, message.Message);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(SocketMessage socketMessage)
    {
        if (socketMessage.Author.IsBot)
            return;

        if (socketMessage.Channel is not SocketGuildChannel guildChannel)
            return;

        var channelId = guildChannel.Id.ToString();
        var allowList = options.Value.AllowedChannelIds;

        if (allowList.Count > 0 && !allowList.Contains(channelId))
            return;

        _activeRoomIds[channelId] = 0;

        if (MessageReceived == null)
            return;

        var mapped = new DiscordInboundMessage
        {
            ChannelId = channelId,
            ChannelName = guildChannel.Name,
            GuildName = guildChannel.Guild.Name,
            AuthorId = socketMessage.Author.Id.ToString(),
            AuthorName = socketMessage.Author.Username,
            Content = socketMessage.Content,
            MessageId = socketMessage.Id.ToString(),
            IsGroupChat = true,
            TimestampUnixMilliseconds = socketMessage.Timestamp.ToUnixTimeMilliseconds()
        };

        await MessageReceived.Invoke(mapped);
    }
}

