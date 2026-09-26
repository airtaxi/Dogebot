using Dogebot.Commons;
using Dogebot.LocoClient.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Dogebot.LocoClient.Services;

/// <summary>
/// Calls the Dogebot.Server endpoints (/notify, /command) with the shared API key.
/// </summary>
public class DogebotServerApiClient(HttpClient httpClient, IOptions<LocoClientOptions> options, ILogger<DogebotServerApiClient> logger) : IDogebotServerApiClient
{
    public async Task<ServerResponse> NotifyAsync(ServerNotification notification, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUrl("notify")) { Content = JsonContent.Create(notification) };
        ApplyApiKeyHeader(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[LOCO_SERVER] Notify failed: {StatusCode}", response.StatusCode);
            return new ServerResponse { Action = "error", Message = $"Notify failed: {(int)response.StatusCode}" };
        }

        return await response.Content.ReadFromJsonAsync<ServerResponse>(cancellationToken: cancellationToken) ?? new ServerResponse();
    }

    public async Task<ServerResponse> GetPendingCommandAsync(IReadOnlyCollection<string> availableRoomIds, CancellationToken cancellationToken)
    {
        var roomIds = availableRoomIds.Where(roomId => !string.IsNullOrWhiteSpace(roomId)).ToArray();
        var query = roomIds.Length == 0 ? string.Empty : $"?availableRooms={Uri.EscapeDataString(string.Join(',', roomIds))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BuildUrl("command")}{query}");
        ApplyApiKeyHeader(request);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[LOCO_SERVER] Command polling failed: {StatusCode}", response.StatusCode);
            return new ServerResponse { Action = "error", Message = $"Polling failed: {(int)response.StatusCode}" };
        }

        return await response.Content.ReadFromJsonAsync<ServerResponse>(cancellationToken: cancellationToken) ?? new ServerResponse();
    }

    private void ApplyApiKeyHeader(HttpRequestMessage request)
    {
        var apiKey = Environment.GetEnvironmentVariable(ApiKeyAuthenticationDefaults.EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.TryAddWithoutValidation(ApiKeyAuthenticationDefaults.HeaderName, apiKey);
    }

    private string BuildUrl(string path) => $"{options.Value.ServerBaseUrl.TrimEnd('/')}/{path}";
}
