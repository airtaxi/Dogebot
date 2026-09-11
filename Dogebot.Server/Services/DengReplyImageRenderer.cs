using System.Globalization;
using System.Text;
using SkiaSharp;

namespace Dogebot.Server.Services;

public class DengReplyImageRenderer : IDengReplyImageRenderer
{
    private const string FontFileName = "SUIT-Variable.ttf";
    private const string Ellipsis = "…";
    private const int MaximumPreviewCharacterCount = 250;
    private const int WordBoundarySearchLength = 24;
    private const int ImageWidth = 1200;
    private const int ImageHeight = 630;
    private const int PngQuality = 100;
    private const float BorderThickness = 32f;
    private const float CardCornerRadius = 28f;
    private const float TextHorizontalPadding = 72f;
    private const float TextVerticalPadding = 64f;
    private const float MaximumFontSize = 56f;
    private const float MinimumFontSize = 26f;
    private const float FontSizeStep = 2f;
    private const float LineHeightMultiplier = 1.42f;
    private const float FontWeight = 600f;
    private static readonly SKColor s_borderColor = new(0x00, 0x80, 0x80);
    private static readonly SKColor s_cardColor = SKColors.White;
    private static readonly SKColor s_textColor = new(0x22, 0x22, 0x22);
    private static readonly object s_typefaceLock = new();
    private static SKTypeface? s_typeface;
    private static bool s_typefaceLoadCompleted;

    private readonly ILogger<DengReplyImageRenderer> _logger;

    public DengReplyImageRenderer(ILogger<DengReplyImageRenderer> logger)
    {
        _logger = logger;
        GetTypeface();
    }

    public byte[] Render(string content)
    {
        using var surface = SKSurface.Create(new SKImageInfo(ImageWidth, ImageHeight));
        if (surface is null) throw new InvalidOperationException("Unable to create the reply preview image surface.");

        var canvas = surface.Canvas;
        canvas.Clear(s_borderColor);

        var cardRect = new SKRect(BorderThickness, BorderThickness, ImageWidth - BorderThickness, ImageHeight - BorderThickness);
        using var cardPaint = new SKPaint { Color = s_cardColor, IsAntialias = true };
        canvas.DrawRoundRect(cardRect, CardCornerRadius, CardCornerRadius, cardPaint);

        var typeface = GetTypeface();
        if (typeface is not null) DrawContent(canvas, content, typeface, cardRect);

        using var image = surface.Snapshot();
        using var imageData = image.Encode(SKEncodedImageFormat.Png, PngQuality);
        return imageData.ToArray();
    }

    private static void DrawContent(SKCanvas canvas, string content, SKTypeface typeface, SKRect cardRect)
    {
        var availableWidth = cardRect.Width - TextHorizontalPadding * 2;
        var availableHeight = cardRect.Height - TextVerticalPadding * 2;

        using var probeFont = new SKFont(typeface, MinimumFontSize);
        var displayText = PrepareText(content, probeFont);
        if (displayText.Length == 0) return;

        var (fontSize, lines) = FitText(displayText, typeface, availableWidth, availableHeight);
        using var font = new SKFont(typeface, fontSize) { Edging = SKFontEdging.Antialias };
        using var textPaint = new SKPaint { Color = s_textColor, IsAntialias = true };

        var metrics = font.Metrics;
        var lineHeight = fontSize * LineHeightMultiplier;
        var textBlockHeight = (lines.Count - 1) * lineHeight + (metrics.Descent - metrics.Ascent);
        var baseline = cardRect.MidY - textBlockHeight / 2 - metrics.Ascent;

        foreach (var line in lines)
        {
            canvas.DrawText(line, cardRect.MidX, baseline, SKTextAlign.Center, font, textPaint);
            baseline += lineHeight;
        }
    }

    private static (float FontSize, List<string> Lines) FitText(string text, SKTypeface typeface, float availableWidth, float availableHeight)
    {
        for (var candidateFontSize = MaximumFontSize; candidateFontSize >= MinimumFontSize; candidateFontSize -= FontSizeStep)
        {
            using var candidateFont = new SKFont(typeface, candidateFontSize);
            var candidateLines = WrapText(text, candidateFont, availableWidth);
            var candidateMetrics = candidateFont.Metrics;
            var candidateHeight = (candidateLines.Count - 1) * candidateFontSize * LineHeightMultiplier + (candidateMetrics.Descent - candidateMetrics.Ascent);
            if (candidateHeight <= availableHeight) return (candidateFontSize, candidateLines);
        }

        using var minimumFont = new SKFont(typeface, MinimumFontSize);
        var minimumMetrics = minimumFont.Metrics;
        var minimumGlyphHeight = minimumMetrics.Descent - minimumMetrics.Ascent;
        var minimumLineHeight = MinimumFontSize * LineHeightMultiplier;
        var maximumLineCount = Math.Max(1, (int)((availableHeight - minimumGlyphHeight) / minimumLineHeight) + 1);
        var minimumLines = WrapText(text, minimumFont, availableWidth);
        return (MinimumFontSize, TruncateLines(minimumLines, maximumLineCount, minimumFont, availableWidth));
    }

    private static List<string> WrapText(string text, SKFont font, float maximumWidth)
    {
        var lines = new List<string>();

        foreach (var paragraph in text.Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            var currentLine = string.Empty;

            foreach (var word in paragraph.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var candidateLine = currentLine.Length == 0 ? word : $"{currentLine} {word}";
                if (font.MeasureText(candidateLine) <= maximumWidth)
                {
                    currentLine = candidateLine;
                    continue;
                }

                if (currentLine.Length > 0)
                {
                    lines.Add(currentLine);
                    currentLine = string.Empty;
                }

                if (font.MeasureText(word) <= maximumWidth)
                {
                    currentLine = word;
                    continue;
                }

                var remainingWord = word;
                while (remainingWord.Length > 0)
                {
                    var fitCount = font.BreakText(remainingWord, maximumWidth);
                    if (fitCount <= 0) fitCount = 1;
                    if (fitCount >= remainingWord.Length)
                    {
                        currentLine = remainingWord;
                        break;
                    }

                    lines.Add(remainingWord[..fitCount]);
                    remainingWord = remainingWord[fitCount..];
                }
            }

            if (currentLine.Length > 0) lines.Add(currentLine);
        }

        return lines;
    }

    private static List<string> TruncateLines(List<string> lines, int maximumLineCount, SKFont font, float maximumWidth)
    {
        if (lines.Count <= maximumLineCount) return lines;

        var truncatedLines = lines.GetRange(0, maximumLineCount);
        truncatedLines[^1] = AppendEllipsis(truncatedLines[^1], font, maximumWidth);
        return truncatedLines;
    }

    private static string AppendEllipsis(string line, SKFont font, float maximumWidth)
    {
        if (font.MeasureText(line + Ellipsis) <= maximumWidth) return line + Ellipsis;

        var trimmedLine = line;
        while (trimmedLine.Length > 0 && font.MeasureText(trimmedLine + Ellipsis) > maximumWidth) trimmedLine = trimmedLine[..^1];
        return trimmedLine + Ellipsis;
    }

    private static string PrepareText(string content, SKFont font)
    {
        var normalizedText = content.ReplaceLineEndings("\n").Replace('\t', ' ').Trim();
        var preparedText = new StringBuilder(normalizedText.Length);

        foreach (var rune in normalizedText.EnumerateRunes())
        {
            if (IsPrintableGlyph(font, rune))
            {
                preparedText.Append(rune.ToString());
            }
        }

        return TruncateForPreview(preparedText.ToString());
    }

    private static string TruncateForPreview(string text)
    {
        var textElementIndexes = StringInfo.ParseCombiningCharacters(text);
        if (textElementIndexes.Length <= MaximumPreviewCharacterCount) return text;

        var cutIndex = textElementIndexes[MaximumPreviewCharacterCount];
        var previewText = text[..cutIndex];
        var lastWhitespaceIndex = previewText.LastIndexOf(' ');
        if (lastWhitespaceIndex >= 0 && cutIndex - lastWhitespaceIndex <= WordBoundarySearchLength) previewText = previewText[..lastWhitespaceIndex];
        return $"{previewText.TrimEnd()}{Ellipsis}";
    }

    private static bool IsPrintableGlyph(SKFont font, Rune rune) =>
        rune.Value == '\n' || font.ContainsGlyph(rune.Value);

    private SKTypeface? GetTypeface()
    {
        if (s_typefaceLoadCompleted) return s_typeface;

        lock (s_typefaceLock)
        {
            if (s_typefaceLoadCompleted) return s_typeface;
            s_typeface = LoadTypeface();
            s_typefaceLoadCompleted = true;
            return s_typeface;
        }
    }

    private SKTypeface? LoadTypeface()
    {
        try
        {
            var fontPath = Path.Combine(AppContext.BaseDirectory, "Assets", FontFileName);
            if (!File.Exists(fontPath))
            {
                _logger.LogError("[DENG_REPLY_IMAGE] Font file not found at {FontPath}. Preview images will be rendered without text.", fontPath);
                return null;
            }

            using var fontData = SKData.CreateCopy(File.ReadAllBytes(fontPath));
            var baseTypeface = SKTypeface.FromData(fontData);
            if (baseTypeface is null)
            {
                _logger.LogError("[DENG_REPLY_IMAGE] Failed to parse font file at {FontPath}. Preview images will be rendered without text.", fontPath);
                return null;
            }

            var weightedTypeface = CreateWeightedTypeface(baseTypeface, FontWeight);
            if (weightedTypeface is null) return baseTypeface;
            baseTypeface.Dispose();
            return weightedTypeface;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[DENG_REPLY_IMAGE] Failed to load font file {FontFileName}. Preview images will be rendered without text.", FontFileName);
            return null;
        }
    }

    private static SKTypeface? CreateWeightedTypeface(SKTypeface typeface, float fontWeight)
    {
        try
        {
            var weightAxis = typeface.VariationDesignParameters.FirstOrDefault(axis => axis.Tag.ToString() == "wght");
            if (weightAxis.Tag == default) return null;
            return typeface.Clone([new SKFontVariationPositionCoordinate { Axis = weightAxis.Tag, Value = fontWeight }]);
        }
        catch (Exception) { return null; }
    }
}
