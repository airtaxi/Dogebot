using Dogebot.LocoClient.Configuration;
using Dogebot.LocoClient.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Dogebot.LocoClient.Services;

/// <summary>
/// HTTP and WebSocket client for the kakao-cli API mode server.
/// </summary>
public class LocoCliApiClient(IOptions<LocoClientOptions> options, ILogger<LocoCliApiClient> logger) : ILocoCliApiClient
{
    private const int ReceiveBufferSize = 8192;

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public async Task<IReadOnlyList<LocoRoom>> GetRoomsAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(BuildApiUrl("api/rooms"), cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<LocoRoomsResponse>(cancellationToken: cancellationToken);
        return payload?.Rooms ?? [];
    }

    public async Task SendMessageAsync(string roomId, string message, CancellationToken cancellationToken)
    {
        using var content = JsonContent.Create(new LocoSendMessageRequest { Message = message });

        using var response = await _httpClient.PostAsync(BuildApiUrl($"api/rooms/{Uri.EscapeDataString(roomId)}/messages"), content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RunRoomStreamAsync(string roomId, Func<LocoRoomMessage, LocoRoom?, Task> onMessageReceived, CancellationToken cancellationToken)
    {
        var reconnectDelay = TimeSpan.FromSeconds(Math.Max(1, options.Value.WebSocketReconnectDelaySeconds));
        var streamUri = BuildStreamUri(roomId);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var webSocket = new ClientWebSocket();
                webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);
                webSocket.Options.KeepAliveTimeout = TimeSpan.FromSeconds(10);

                await webSocket.ConnectAsync(streamUri, cancellationToken);
                logger.LogInformation("[LOCO_CLI] WebSocket connected. room={RoomId}", roomId);

                await ReceiveMessagesAsync(webSocket, onMessageReceived, cancellationToken);
                logger.LogWarning("[LOCO_CLI] WebSocket closed by the CLI server. room={RoomId}", roomId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogWarning("[LOCO_CLI] WebSocket error. room={RoomId}, message={Message}. Reconnecting in {DelaySeconds}s", roomId, exception.Message, reconnectDelay.TotalSeconds); }

            try { await Task.Delay(reconnectDelay, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ReceiveMessagesAsync(ClientWebSocket webSocket, Func<LocoRoomMessage, LocoRoom?, Task> onMessageReceived, CancellationToken cancellationToken)
    {
        Memory<byte> buffer = new byte[ReceiveBufferSize];
        using var messageBuffer = new MemoryStream();

        while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await webSocket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close) return;
            if (result.MessageType != WebSocketMessageType.Text) continue;

            messageBuffer.Write(buffer.Span[..result.Count]);
            if (!result.EndOfMessage) continue;

            var payload = Encoding.UTF8.GetString(messageBuffer.ToArray());
            messageBuffer.SetLength(0);
            await HandleEnvelopeAsync(payload, onMessageReceived);
        }
    }

    private async Task HandleEnvelopeAsync(string payload, Func<LocoRoomMessage, LocoRoom?, Task> onMessageReceived)
    {
        LocoRoomMessageEnvelope? envelope;
        try { envelope = JsonSerializer.Deserialize<LocoRoomMessageEnvelope>(payload); }
        catch (JsonException exception) { logger.LogWarning(exception, "[LOCO_CLI] Failed to parse a WebSocket payload."); return; }

        if (envelope is null || envelope.Type != "message" || envelope.Message is null) return;

        await onMessageReceived(envelope.Message, envelope.Room);
    }

    private string BuildApiUrl(string path) => $"{options.Value.CliBaseUrl.TrimEnd('/')}/{path}";

    private Uri BuildStreamUri(string roomId)
    {
        var baseUri = new Uri(options.Value.CliBaseUrl, UriKind.Absolute);
        var scheme = baseUri.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        return new Uri($"{scheme}://{baseUri.Authority}/ws/rooms/{Uri.EscapeDataString(roomId)}");
    }
}
