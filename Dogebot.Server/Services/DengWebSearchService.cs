using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dogebot.Server.Services;

public class DengWebSearchService(IHttpClientFactory httpClientFactory, ILogger<DengWebSearchService> logger) : IDengWebSearchService
{
    private const string TavilyApiKeyEnvironmentVariableName = "DOGEBOT_DENG_AI_TAVILY_API_KEY";
    private const string TavilySearchUrl = "https://api.tavily.com/search";
    private const int DefaultMaximumResultCount = 3;
    private const int MinimumResultCount = 1;
    private const int MaximumResultCount = 5;
    private const int MaximumContentCharacterCount = 280;

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string? _apiKey = Environment.GetEnvironmentVariable(TavilyApiKeyEnvironmentVariableName);

    private async Task<TavilySearchResponse?> SearchAsync(string query, int maximumResultCount, string topic, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("[DENG_WEB_SEARCH] Tavily API key is not configured. Required environment variable: {EnvironmentVariableName}", TavilyApiKeyEnvironmentVariableName);
            return null;
        }

        var requestBody = new
        {
            query,
            search_depth = "basic",
            max_results = maximumResultCount,
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
                LogSearchFailure(response.StatusCode);
                return null;
            }

            await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonSerializer.DeserializeAsync<TavilySearchResponse>(responseStream, DengAiToolJson.SerializerOptions, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or NotSupportedException)
        {
            logger.LogWarning(exception, "[DENG_WEB_SEARCH] Tavily search request failed");
            return null;
        }
    }

    private void LogSearchFailure(HttpStatusCode statusCode)
    {
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or (HttpStatusCode)429)
        {
            logger.LogWarning("[DENG_WEB_SEARCH] Tavily search request failed with status code {StatusCode}", statusCode);
            return;
        }

        logger.LogWarning("[DENG_WEB_SEARCH] Tavily search request failed with status code {StatusCode}", statusCode);
    }

    private static string CreateSearchFailureToolResult() =>
        DengAiToolJson.Serialize(new { Message = "검색 결과를 가져오지 못했습니다. 검색 실패, 인증 실패, 레이트 리밋은 사용자에게 말하지 말고 일반 지식으로 자연스럽게 답변하세요." });

    private static string NormalizeTopic(string? topic) =>
        string.Equals(topic, "news", StringComparison.OrdinalIgnoreCase) ? "news" : "general";

    private static string TrimContent(string content)
    {
        if (content.Length <= MaximumContentCharacterCount) return content;

        return content[..MaximumContentCharacterCount].TrimEnd();
    }

    private sealed record TavilySearchResponse([property: JsonPropertyName("query")] string Query, [property: JsonPropertyName("results")] IReadOnlyList<TavilySearchResult> Results);

    private sealed record TavilySearchResult([property: JsonPropertyName("title")] string? Title, [property: JsonPropertyName("content")] string? Content, [property: JsonPropertyName("score")] double? Score);

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("search_web", "Search the web with Tavily when the user asks about current, external, local, restaurant, product, game, character, service, or place information that Dogebot may not know. Do not mention search failures, authentication failures, or rate limits to the user; answer naturally from general knowledge if this tool returns no usable results.", CreateSearchSchema())
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("search_web", StringComparison.Ordinal)) return "Unknown web search tool.";

        var query = DengAiToolJson.ReadString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query)) return CreateSearchFailureToolResult();

        var maximumResultCount = Math.Clamp(DengAiToolJson.ReadInt32(arguments, "maxResults") ?? DefaultMaximumResultCount, MinimumResultCount, MaximumResultCount);
        var topic = NormalizeTopic(DengAiToolJson.ReadString(arguments, "topic"));
        var searchResponse = await SearchAsync(query, maximumResultCount, topic, cancellationToken);
        if (searchResponse?.Results.Count > 0 != true) return CreateSearchFailureToolResult();

        var results = searchResponse.Results
            .Where(result => !string.IsNullOrWhiteSpace(result.Title) || !string.IsNullOrWhiteSpace(result.Content))
            .Take(maximumResultCount)
            .Select(result => new
            {
                Title = result.Title ?? string.Empty,
                Content = TrimContent(result.Content ?? string.Empty)
            })
            .ToList();
        if (results.Count == 0) return CreateSearchFailureToolResult();

        return DengAiToolJson.Serialize(new
        {
            searchResponse.Query,
            Results = results
        });
    }

    private static DengAiJsonSchema CreateSearchSchema() =>
        DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["query"] = DengAiJsonSchemaProperty.String("Search query to look up current or external information."),
            ["maxResults"] = DengAiJsonSchemaProperty.Integer("Maximum number of search results. Allowed range is 1 to 5. Defaults to 3.", MinimumResultCount, MaximumResultCount),
            ["topic"] = DengAiJsonSchemaProperty.String("Search topic. Use general by default, or news for recent news-like queries.", ["general", "news"])
        }, ["query"]);

    #endregion
}
