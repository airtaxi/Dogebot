using System.Net;
using Dogebot.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace Dogebot.Server.Controllers;

[Route("deng")]
public class DengReplyController(IDengAiLongReplyService dengAiLongReplyService) : ControllerBase
{
    private const string PageTitle = "도지봇 AI 답변";

    [HttpGet("{urlHash}")]
    public async Task<IActionResult> Get([FromRoute] string urlHash)
    {
        var content = await dengAiLongReplyService.GetContentByUrlHashAsync(urlHash);
        if (string.IsNullOrEmpty(content)) return NotFound();

        var pageUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}";
        return Content(BuildHtmlPage(pageUrl, content), "text/html; charset=utf-8");
    }

    private static string BuildHtmlPage(string pageUrl, string content)
    {
        var escapedTitle = WebUtility.HtmlEncode(PageTitle);
        var escapedDescription = WebUtility.HtmlEncode(content.Replace("\r", " ").Replace("\n", " "));
        var escapedUrl = WebUtility.HtmlEncode(pageUrl);
        var escapedContent = WebUtility.HtmlEncode(content);

        return $"""
            <!DOCTYPE html>
            <html lang="ko">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta property="og:title" content="{escapedTitle}">
            <meta property="og:description" content="{escapedDescription}">
            <meta property="og:type" content="article">
            <meta property="og:url" content="{escapedUrl}">
            <title>{escapedTitle}</title>
            <style>
            body {{ font-family: -apple-system, sans-serif; max-width: 720px; margin: 0 auto; padding: 16px; line-height: 1.6; overflow-wrap: break-word; }}
            </style>
            </head>
            <body>
            <pre>{escapedContent}</pre>
            </body>
            </html>
            """;
    }
}