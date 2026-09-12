using System.Globalization;
using System.Text;
using SkiaSharp;

namespace Dogebot.Server.Services;

public class DengReplyImageRenderer : IDengReplyImageRenderer
{
    private const string FontFileName = "SUIT-Variable.ttf";
    private const string Ellipsis = "…";
    private const int MaximumPreviewCharacterCount = 150;
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
    private const float BlankLineHeightDivisor = 2f;
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
        var textBlockHeight = MeasureTextBlockHeight(lines, lineHeight, metrics);
        var baseline = cardRect.MidY - textBlockHeight / 2 - metrics.Ascent;

        foreach (var line in lines)
        {
            if (line.Length > 0) canvas.DrawText(line, cardRect.MidX, baseline, SKTextAlign.Center, font, textPaint);
            baseline += GetBaselineAdvance(line, lineHeight);
        }
    }

    private static (float FontSize, List<string> Lines) FitText(string text, SKTypeface typeface, float availableWidth, float availableHeight)
    {
        for (var candidateFontSize = MaximumFontSize; candidateFontSize >= MinimumFontSize; candidateFontSize -= FontSizeStep)
        {
            using var candidateFont = new SKFont(typeface, candidateFontSize);
            var candidateLines = WrapText(text, candidateFont, availableWidth);
            var candidateLineHeight = candidateFontSize * LineHeightMultiplier;
            var candidateHeight = MeasureTextBlockHeight(candidateLines, candidateLineHeight, candidateFont.Metrics);
            if (candidateHeight <= availableHeight) return (candidateFontSize, candidateLines);
        }

        using var minimumFont = new SKFont(typeface, MinimumFontSize);
        var minimumLineHeight = MinimumFontSize * LineHeightMultiplier;
        var minimumLines = WrapText(text, minimumFont, availableWidth);
        var maximumLineCount = GetFittingLineCount(minimumLines, minimumLineHeight, minimumFont.Metrics, availableHeight);
        return (MinimumFontSize, TruncateLines(minimumLines, maximumLineCount, minimumFont, availableWidth));
    }

    private static float MeasureTextBlockHeight(List<string> lines, float lineHeight, SKFontMetrics metrics)
    {
        var textBlockHeight = metrics.Descent - metrics.Ascent;
        for (var lineIndex = 0; lineIndex < lines.Count - 1; lineIndex++) textBlockHeight += GetBaselineAdvance(lines[lineIndex], lineHeight);
        return textBlockHeight;
    }

    private static int GetFittingLineCount(List<string> lines, float lineHeight, SKFontMetrics metrics, float availableHeight)
    {
        var availableAdvanceHeight = availableHeight - (metrics.Descent - metrics.Ascent);
        var accumulatedHeight = 0f;
        var lineCount = 1;
        while (lineCount < lines.Count)
        {
            var baselineAdvance = GetBaselineAdvance(lines[lineCount - 1], lineHeight);
            if (accumulatedHeight + baselineAdvance > availableAdvanceHeight) break;
            accumulatedHeight += baselineAdvance;
            lineCount++;
        }

        return lineCount;
    }

    private static float GetBaselineAdvance(string line, float lineHeight) => line.Length == 0 ? lineHeight / BlankLineHeightDivisor : lineHeight;

    private static List<string> WrapText(string text, SKFont font, float maximumWidth)
    {
        var lines = new List<string>();

        foreach (var paragraph in text.Split('\n'))
        {
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
            else lines.Add(string.Empty);
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
        var normalizedText = content.ReplaceLineEndings("\n").Trim();
        var preparedText = new StringBuilder(normalizedText.Length);

        foreach (var rune in normalizedText.EnumerateRunes())
        {
            if (rune.Value == '\n') preparedText.Append('\n');
            else if (rune.Value == '\t') preparedText.Append(' ');
            else if (font.ContainsGlyph(rune.Value)) preparedText.Append(rune.ToString());
        }

        return TruncateForPreview(preparedText.ToString());
    }

    private static string TruncateForPreview(string text)
    {
        var textElementIndexes = StringInfo.ParseCombiningCharacters(text);
        if (textElementIndexes.Length <= MaximumPreviewCharacterCount) return text;

        var cutIndex = textElementIndexes[MaximumPreviewCharacterCount];
        var previewText = text[..cutIndex];
        var lastBoundaryIndex = previewText.LastIndexOfAny([' ', '\n']);
        if (lastBoundaryIndex >= 0 && cutIndex - lastBoundaryIndex <= WordBoundarySearchLength) previewText = previewText[..lastBoundaryIndex];
        return $"{previewText.TrimEnd()}{Ellipsis}";
    }

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
