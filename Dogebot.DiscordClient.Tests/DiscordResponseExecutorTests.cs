using Dogebot.Commons;
using Dogebot.DiscordClient.Adapters;
using Dogebot.DiscordClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dogebot.DiscordClient.Tests;

public class DiscordResponseExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SendText_DelegatesToGateway()
    {
        var fakeGateway = new FakeGatewayClient();
        var executor = new DiscordResponseExecutor(fakeGateway, NullLogger<DiscordResponseExecutor>.Instance);

        await executor.ExecuteAsync(new ServerResponse
        {
            Action = "send_text",
            RoomId = "10",
            Message = "pong"
        }, CancellationToken.None);

        Assert.Equal("10", fakeGateway.LastChannelId);
        Assert.Equal("pong", fakeGateway.LastMessage);
    }

    [Fact]
    public async Task ExecuteAsync_Read_DoesNotSendMessage()
    {
        var fakeGateway = new FakeGatewayClient();
        var executor = new DiscordResponseExecutor(fakeGateway, NullLogger<DiscordResponseExecutor>.Instance);

        await executor.ExecuteAsync(new ServerResponse { Action = "read", RoomId = "10" }, CancellationToken.None);

        Assert.Null(fakeGateway.LastChannelId);
        Assert.Null(fakeGateway.LastMessage);
    }

    private class FakeGatewayClient : IDiscordGatewayClient
    {
#pragma warning disable CS0067
        public event Func<Dogebot.DiscordClient.Models.DiscordInboundMessage, Task>? MessageReceived;
#pragma warning restore CS0067

        public string? LastChannelId { get; private set; }
        public string? LastMessage { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SendMessageAsync(string channelId, string message, CancellationToken cancellationToken)
        {
            LastChannelId = channelId;
            LastMessage = message;
            return Task.CompletedTask;
        }

        public IReadOnlyList<string> GetAvailableRoomIds() => [];
    }
}


