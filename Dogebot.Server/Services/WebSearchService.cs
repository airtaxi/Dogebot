using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dogebot.Server.Services;

public class WebSearchService(IHttpClientFactory httpClientFactory, ILogger<WebSearchService> logger)
{
    private const string TavilyApiKeyEnvironmentVariableName = "DOGEBOT_DENG_AI_TAVILY_API_KEY";
    private const string TavilySearchUrl = "https://api.tavily.com/search";
    internal const int DefaultMaximumResultCount = 10;
    internal const int MinimumResultCount = 1;
    internal const int MaximumResultCount = 10;

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string? _apiKey = Environment.GetEnvironmentVariable(TavilyApiKeyEnvironmentVariableName);

    public static string NormalizeTopic(string? topic) =>
        string.Equals(topic, "news", StringComparison.OrdinalIgnoreCase) ? "news" : "general";

    public async Task<IReadOnlyList<WebSearchResult>?> SearchAsync(string query, int maximumResultCount, string topic, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("[WEB_SEARCH] Tavily API key is not configured. Required environment variable: {EnvironmentVariableName}", TavilyApiKeyEnvironmentVariableName);
            return null;
        }

        var clampedResultCount = Math.Clamp(maximumResultCount, MinimumResultCount, MaximumResultCount);

        var requestBody = new
        {
            query,
            search_depth = "basic",
            max_results = clampedResultCount,
            include_answer = false,
            include_raw_content = false,
            include_images = false,
            topic
        };

        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, TavilySearchUrl)
        {
            Content = JsonContent.Create(requestBody, options: DengAiToolJson.SerializerOptions)
        };
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        try
        {
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("[WEB_SEARCH] Tavily search request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var searchResponse = await JsonSerializer.DeserializeAsync<TavilySearchResponse>(responseStream, DengAiToolJson.SerializerOptions, cancellationToken);
            return searchResponse?.Results
                .Where(result => !string.IsNullOrWhiteSpace(result.Title) || !string.IsNullOrWhiteSpace(result.Content))
                .Select(result => new WebSearchResult(result.Title, result.Url, result.Content, result.Score))
                .ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or NotSupportedException)
        {
            logger.LogWarning(exception, "[WEB_SEARCH] Tavily search request failed");
            return null;
        }
    }

    private sealed record TavilySearchResponse([property: JsonPropertyName("query")] string Query, [property: JsonPropertyName("results")] IReadOnlyList<TavilySearchResult> Results);

    private sealed record TavilySearchResult([property: JsonPropertyName("title")] string? Title, [property: JsonPropertyName("url")] string? Url, [property: JsonPropertyName("content")] string? Content, [property: JsonPropertyName("score")] double? Score);
}