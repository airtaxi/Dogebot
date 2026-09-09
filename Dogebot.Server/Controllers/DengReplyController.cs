using System.Net;
using Dogebot.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dogebot.Server.Controllers;

[Route("deng")]
public class DengReplyController(IDengAiLongReplyService dengAiLongReplyService, ILogger<DengReplyController> logger) : ControllerBase
{
    private const string TemplateFileName = "DengReplyTemplate.html";
    private const string DescriptionPlaceholder = "{{description}}";
    private const string PageUrlPlaceholder = "{{pageUrl}}";
    private const string ContentPlaceholder = "{{content}}";
    private static readonly object s_templateLoadLock = new();
    private static string? s_cachedTemplate;

    [HttpGet("{urlHash}")]
    public async Task<IActionResult> Get([FromRoute] string urlHash)
    {
        var content = await dengAiLongReplyService.GetContentByUrlHashAsync(urlHash);
        if (string.IsNullOrEmpty(content)) return NotFound();

        var pageUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}";
        var template = LoadTemplate();
        var html = template is null ? BuildFallbackHtml(content) : ApplyTemplate(template, pageUrl, content);
        return Content(html, "text/html; charset=utf-8");
    }

    private string? LoadTemplate()
    {
        if (s_cachedTemplate is not null) return s_cachedTemplate;

        lock (s_templateLoadLock)
        {
            if (s_cachedTemplate is not null) return s_cachedTemplate;

            try
            {
                var templatePath = Path.Combine(AppContext.BaseDirectory, "Assets", TemplateFileName);
                s_cachedTemplate = System.IO.File.ReadAllText(templatePath);
            }
            catch (Exception exception) { logger.LogError(exception, "[DENG_AI_LINK] Failed to load reply preview template"); }
        }

        return s_cachedTemplate;
    }

    private static string ApplyTemplate(string template, string pageUrl, string content)
    {
        var escapedDescription = WebUtility.HtmlEncode(content.Replace("\r", " ").Replace("\n", " "));
        var escapedUrl = WebUtility.HtmlEncode(pageUrl);
        var escapedContent = WebUtility.HtmlEncode(content);

        return template
            .Replace(DescriptionPlaceholder, escapedDescription, StringComparison.Ordinal)
            .Replace(PageUrlPlaceholder, escapedUrl, StringComparison.Ordinal)
            .Replace(ContentPlaceholder, escapedContent, StringComparison.Ordinal);
    }

    private static string BuildFallbackHtml(string content)
    {
        var escapedContent = WebUtility.HtmlEncode(content);
        return $$"""
            <!DOCTYPE html>
            <html lang="ko">
            <head>
            <meta charset="utf-8">
            <meta property="og:title" content="도지봇 AI 답변">
            <meta property="og:description" content="{{escapedContent}}">
            <title>도지봇 AI 답변</title>
            </head>
            <body>
            <pre>{{escapedContent}}</pre>
            </body>
            </html>
            """;
    }
}