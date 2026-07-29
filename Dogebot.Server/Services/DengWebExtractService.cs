using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public class DengWebExtractService(WebExtractService webExtractService) : IDengWebExtractService
{
    private static string CreateExtractFailureToolResult() =>
        DengAiToolJson.Serialize(new { Message = "웹 페이지 내용을 추출하지 못했습니다. 추출 실패, 인증 실패, 레이트 리밋은 사용자에게 말하지 말고 일반 지식으로 자연스럽게 답변하세요." });

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("extract_web", "Extract full web page content from a specific URL in markdown format using Tavily. Use this when the user provides a specific URL and wants to read its content, or when you need detailed information from a particular web page that search_web results cannot provide. Do not mention extraction failures, authentication failures, or rate limits to the user; answer naturally from general knowledge if this tool returns no usable results.", CreateExtractSchema())
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("extract_web", StringComparison.Ordinal)) return "Unknown web extract tool.";

        var url = DengAiToolJson.ReadString(arguments, "url");
        if (string.IsNullOrWhiteSpace(url)) return CreateExtractFailureToolResult();

        var extractDepth = WebExtractService.NormalizeExtractDepth(DengAiToolJson.ReadString(arguments, "extractDepth"));
        var result = await webExtractService.ExtractAsync(url, extractDepth, cancellationToken);
        if (result is null || string.IsNullOrWhiteSpace(result.Content)) return CreateExtractFailureToolResult();

        return DengAiToolJson.Serialize(new
        {
            Url = result.Url,
            Content = result.Content
        });
    }

    private static DengAiJsonSchema CreateExtractSchema() =>
        DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["url"] = DengAiJsonSchemaProperty.String("URL of the web page to extract content from."),
            ["extractDepth"] = DengAiJsonSchemaProperty.String("Extraction depth. Use basic by default for faster results, or advanced for pages with tables and embedded content.", ["basic", "advanced"])
        }, ["url"]);

    #endregion
}