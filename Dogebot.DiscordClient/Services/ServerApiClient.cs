using Dogebot.Commons;
using Dogebot.DiscordClient.Configuration;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Dogebot.DiscordClient.Services;

public class ServerApiClient(HttpClient httpClient, IOptions<DiscordClientOptions> options, ILogger<ServerApiClient> logger) : IServerApiClient
{
    public async Task<ServerResponse> NotifyAsync(ServerNotification notification, CancellationToken cancellationToken)
    {
        var endpoint = BuildUrl("notify");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(notification) };
        ApplyApiKeyHeader(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[SERVER_API] Notify failed: {StatusCode}", response.StatusCode);
            return new ServerResponse { Action = "error", Message = $"Notify failed: {(int)response.StatusCode}" };
        }

        return await response.Content.ReadFromJsonAsync<ServerResponse>(cancellationToken: cancellationToken) ?? new ServerResponse();
    }

    public async Task<ServerResponse> GetPendingCommandAsync(IEnumerable<string> availableRoomIds, CancellationToken cancellationToken)
    {
        var roomIds = availableRoomIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        var query = roomIds.Length == 0 ? string.Empty : $"?availableRooms={string.Join(',', roomIds)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BuildUrl("command")}{query}");
        ApplyApiKeyHeader(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("[SERVER_API] Polling failed: {StatusCode}", response.StatusCode);
            return new ServerResponse { Action = "error", Message = $"Polling failed: {(int)response.StatusCode}" };
        }

        return await response.Content.ReadFromJsonAsync<ServerResponse>(cancellationToken: cancellationToken) ?? new ServerResponse();
    }

    private void ApplyApiKeyHeader(HttpRequestMessage request)
    {
        var apiKey = string.IsNullOrWhiteSpace(options.Value.ApiKey) ? Environment.GetEnvironmentVariable(ApiKeyAuthenticationDefaults.EnvironmentVariableName) : options.Value.ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.TryAddWithoutValidation(ApiKeyAuthenticationDefaults.HeaderName, apiKey);
    }

    private string BuildUrl(string path)
    {
        var baseUrl = options.Value.ServerBaseUrl.TrimEnd('/');
        return $"{baseUrl}/{path}";
    }
}


