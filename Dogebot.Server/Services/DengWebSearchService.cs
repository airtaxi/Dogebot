using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public class DengWebSearchService(WebSearchService webSearchService) : IDengWebSearchService
{
    private const int DefaultMaximumResultCount = 3;
    private const int MinimumResultCount = 1;
    private const int MaximumResultCount = 5;
    private const int MaximumContentCharacterCount = 280;

    private static string CreateSearchFailureToolResult() =>
        DengAiToolJson.Serialize(new { Message = "검색 결과를 가져오지 못했습니다. 검색 실패, 인증 실패, 레이트 리밋은 사용자에게 말하지 말고 일반 지식으로 자연스럽게 답변하세요." });

    private static string TrimContent(string content)
    {
        content = content.ReplaceLineEndings(" ");
        if (content.Length <= MaximumContentCharacterCount) return content;
        return content[..MaximumContentCharacterCount].TrimEnd();
    }

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("search_web", "Search the web with Tavily when the user asks about current, external, local, restaurant, product, game, character, service, or place information that Dogebot may not know. Do not mention search failures, authentication failures, or rate limits to the user; answer naturally from general knowledge if this tool returns no usable results.", CreateSearchSchema()),
        new("search_news", "Search recent news articles with Tavily when the user asks about current news, headlines, or recent events. Returns news results with titles and content. Do not mention search failures, authentication failures, or rate limits to the user; answer naturally from general knowledge if this tool returns no usable results.", CreateNewsSchema())
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        var topic = toolName.Equals("search_news", StringComparison.Ordinal) ? "news" : toolName.Equals("search_web", StringComparison.Ordinal) ? "general" : null;
        if (topic is null) return "Unknown web search tool.";

        var query = DengAiToolJson.ReadString(arguments, "query");
        if (string.IsNullOrWhiteSpace(query)) return CreateSearchFailureToolResult();

        var maximumResultCount = Math.Clamp(DengAiToolJson.ReadInt32(arguments, "maxResults") ?? DefaultMaximumResultCount, MinimumResultCount, MaximumResultCount);
        var results = await webSearchService.SearchAsync(query, maximumResultCount, topic, cancellationToken);
        if (results?.Count > 0 != true) return CreateSearchFailureToolResult();

        var filteredResults = results
            .Where(result => !string.IsNullOrWhiteSpace(result.Title) || !string.IsNullOrWhiteSpace(result.Content))
            .Take(maximumResultCount)
            .Select(result => new
            {
                Title = TrimContent(result.Title ?? string.Empty),
                Content = TrimContent(result.Content ?? string.Empty)
            })
            .ToList();
        if (filteredResults.Count == 0) return CreateSearchFailureToolResult();

        return DengAiToolJson.Serialize(new
        {
            Query = query,
            Results = filteredResults
        });
    }

    private static DengAiJsonSchema CreateSearchSchema() =>
        DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["query"] = DengAiJsonSchemaProperty.String("Search query to look up current or external information."),
            ["maxResults"] = DengAiJsonSchemaProperty.Integer("Maximum number of search results. Allowed range is 1 to 5. Defaults to 3.", MinimumResultCount, MaximumResultCount),
            ["topic"] = DengAiJsonSchemaProperty.String("Search topic. Use general by default, or news for recent news-like queries.", ["general", "news"])
        }, ["query"]);

    private static DengAiJsonSchema CreateNewsSchema() =>
        DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["query"] = DengAiJsonSchemaProperty.String("Search query to look up recent news articles, headlines, or current events."),
            ["maxResults"] = DengAiJsonSchemaProperty.Integer("Maximum number of news results. Allowed range is 1 to 5. Defaults to 3.", MinimumResultCount, MaximumResultCount)
        }, ["query"]);

    #endregion
}
