using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Dogebot.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Dogebot.Server.Controllers;

[AllowAnonymous]
[Route("deng")]
public partial class DengReplyController(IDengAiLongReplyService dengAiLongReplyService, IDengReplyImageRenderer dengReplyImageRenderer, ILogger<DengReplyController> logger) : ControllerBase
{
    private const string TemplateFileName = "DengReplyTemplate.html";
    private const string PageUrlPlaceholder = "{{pageUrl}}";
    private const string ImageUrlPlaceholder = "{{imageUrl}}";
    private const string ContentPlaceholder = "{{content}}";
    private const string OgImageCacheControl = "public, max-age=259200";
    private static readonly object s_templateLoadLock = new();
    private static string? s_cachedTemplate;

    [HttpGet("{urlHash}")]
    public async Task<IActionResult> Get([FromRoute] string urlHash)
    {
        var content = await dengAiLongReplyService.GetContentByUrlHashAsync(urlHash);
        if (string.IsNullOrEmpty(content)) return NotFound();

        var pageUrl = dengAiLongReplyService.GetReplyUrl(urlHash) ?? $"{Request.Scheme}://{Request.Host}{Request.Path}";
        var template = LoadTemplate();
        var html = template is null ? BuildFallbackHtml(pageUrl, content) : ApplyTemplate(template, pageUrl, content);
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpGet("{urlHash}/image.png")]
    public async Task<IActionResult> GetImage([FromRoute] string urlHash)
    {
        var content = await dengAiLongReplyService.GetContentByUrlHashAsync(urlHash);
        if (string.IsNullOrEmpty(content)) return NotFound();

        try
        {
            var imageBytes = dengReplyImageRenderer.Render(content);
            Response.Headers[HeaderNames.CacheControl] = OgImageCacheControl;
            return File(imageBytes, "image/png");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[DENG_REPLY_IMAGE] Failed to render the reply preview image for {UrlHash}", urlHash);
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
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
        var escapedPageUrl = WebUtility.HtmlEncode(pageUrl);
        var escapedImageUrl = WebUtility.HtmlEncode($"{pageUrl}/image.png");
        var contentHtml = BuildContentHtml(content);

        return template
            .Replace(PageUrlPlaceholder, escapedPageUrl, StringComparison.Ordinal)
            .Replace(ImageUrlPlaceholder, escapedImageUrl, StringComparison.Ordinal)
            .Replace(ContentPlaceholder, contentHtml, StringComparison.Ordinal);
    }

    private static string BuildFallbackHtml(string pageUrl, string content)
    {
        var escapedPageUrl = WebUtility.HtmlEncode(pageUrl);
        var escapedImageUrl = WebUtility.HtmlEncode($"{pageUrl}/image.png");
        var contentHtml = BuildContentHtml(content);
        return $$"""
            <!DOCTYPE html>
            <html lang="ko">
            <head>
            <meta charset="utf-8">
            <meta property="og:title" content="도지봇 AI 답변">
            <meta property="og:type" content="article">
            <meta property="og:url" content="{{escapedPageUrl}}">
            <meta property="og:image" content="{{escapedImageUrl}}">
            <meta property="og:image:width" content="1200">
            <meta property="og:image:height" content="630">
            <title>도지봇 AI 답변</title>
            </head>
            <body>
            <pre>{{contentHtml}}</pre>
            </body>
            </html>
            """;
    }

    private static string BuildContentHtml(string content)
    {
        var contentBuilder = new StringBuilder();
        var lastIndex = 0;

        foreach (Match match in UrlRegex().Matches(content))
        {
            var (url, trailingPunctuation) = SplitTrailingPunctuation(match.Value);
            contentBuilder.Append(WebUtility.HtmlEncode(content[lastIndex..match.Index]));
            contentBuilder.Append(BuildAnchor(url));
            contentBuilder.Append(WebUtility.HtmlEncode(trailingPunctuation));
            lastIndex = match.Index + match.Length;
        }

        contentBuilder.Append(WebUtility.HtmlEncode(content[lastIndex..]));
        return contentBuilder.ToString();
    }

    private static string BuildAnchor(string url)
    {
        var escapedUrl = WebUtility.HtmlEncode(url);
        return $"<a href=\"{escapedUrl}\" target=\"_blank\" rel=\"noopener noreferrer\">{escapedUrl}</a>";
    }

    private static (string Url, string TrailingPunctuation) SplitTrailingPunctuation(string url)
    {
        var trimmedLength = url.Length;
        while (trimmedLength > 0)
        {
            var trailingCharacter = url[trimmedLength - 1];
            if (trailingCharacter is ')' && HasUnmatchedClosingBracket(url, trimmedLength, '(', ')')) { trimmedLength--; continue; }
            if (trailingCharacter is ']' && HasUnmatchedClosingBracket(url, trimmedLength, '[', ']')) { trimmedLength--; continue; }
            if (trailingCharacter is '}' && HasUnmatchedClosingBracket(url, trimmedLength, '{', '}')) { trimmedLength--; continue; }
            if (trailingCharacter is '.' or ',' or ';' or ':' or '!' or '?' or '>' or '」' or '』' or '”' or '’') { trimmedLength--; continue; }
            break;
        }

        return (url[..trimmedLength], url[trimmedLength..]);
    }

    private static bool HasUnmatchedClosingBracket(string url, int length, char openingBracket, char closingBracket)
    {
        var depth = 0;
        for (var index = 0; index < length; index++)
        {
            if (url[index] == openingBracket) depth++;
            else if (url[index] == closingBracket) depth--;
        }

        return depth < 0;
    }

    [GeneratedRegex("""https?://[^\s<>"']+""", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}
