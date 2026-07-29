using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public class WebExtractService(IHttpClientFactory httpClientFactory, ILogger<WebExtractService> logger)
{
    private const string TavilyApiKeyEnvironmentVariableName = "DOGEBOT_DENG_AI_TAVILY_API_KEY";
    private const string TavilyExtractUrl = "https://api.tavily.com/extract";

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string? _apiKey = Environment.GetEnvironmentVariable(TavilyApiKeyEnvironmentVariableName);

    public static string NormalizeExtractDepth(string? extractDepth) =>
        string.Equals(extractDepth, "advanced", StringComparison.OrdinalIgnoreCase) ? "advanced" : "basic";

    public async Task<WebExtractResult?> ExtractAsync(string url, string extractDepth, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("[WEB_EXTRACT] Tavily API key is not configured. Required environment variable: {EnvironmentVariableName}", TavilyApiKeyEnvironmentVariableName);
            return null;
        }

        var requestBody = new
        {
            urls = new[] { url },
            extract_depth = extractDepth,
            format = "markdown",
            include_images = false,
            include_favicon = false
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, TavilyExtractUrl)
        {
            Content = JsonContent.Create(requestBody, options: DengAiToolJson.SerializerOptions)
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("[WEB_EXTRACT] Tavily extract request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var extractResponse = await JsonSerializer.DeserializeAsync<TavilyExtractResponse>(responseStream, DengAiToolJson.SerializerOptions, cancellationToken);
            var result = extractResponse?.Results.FirstOrDefault(static entry => !string.IsNullOrWhiteSpace(entry.Content));
            return result is null ? null : new WebExtractResult(result.Url ?? string.Empty, result.Content ?? string.Empty);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or NotSupportedException)
        {
            logger.LogWarning(exception, "[WEB_EXTRACT] Tavily extract request failed");
            return null;
        }
    }

    private sealed record TavilyExtractResponse([property: JsonPropertyName("results")] IReadOnlyList<TavilyExtractResult> Results);

    private sealed record TavilyExtractResult([property: JsonPropertyName("url")] string? Url, [property: JsonPropertyName("raw_content")] string? Content);
}