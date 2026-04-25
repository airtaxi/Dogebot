using KakaoBotAT.Commons;
using KakaoBotAT.DiscordClient.Configuration;
using KakaoBotAT.DiscordClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace KakaoBotAT.DiscordClient.Tests;

public class ServerApiClientTests
{
    [Fact]
    public async Task NotifyAsync_UsesKakaoNotifyEndpoint()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://localhost/api/kakao/notify", request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(new ServerResponse { Action = "send_text", RoomId = "1", Message = "ok" })
            };
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new DiscordClientOptions { ServerBaseUrl = "https://localhost/api/kakao" });
        var apiClient = new ServerApiClient(httpClient, options, NullLogger<ServerApiClient>.Instance);

        var result = await apiClient.NotifyAsync(new ServerNotification(), CancellationToken.None);

        Assert.Equal("send_text", result.Action);
    }

    [Fact]
    public async Task GetPendingCommandAsync_UsesQueryString()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://localhost/api/kakao/command?availableRooms=100,200", request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent(new ServerResponse { Action = "read", RoomId = "100", Message = string.Empty })
            };
        });

        var httpClient = new HttpClient(handler);
        var options = Options.Create(new DiscordClientOptions { ServerBaseUrl = "https://localhost/api/kakao" });
        var apiClient = new ServerApiClient(httpClient, options, NullLogger<ServerApiClient>.Instance);

        var result = await apiClient.GetPendingCommandAsync(["100", "200"], CancellationToken.None);

        Assert.Equal("read", result.Action);
    }

    private static StringContent JsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}

