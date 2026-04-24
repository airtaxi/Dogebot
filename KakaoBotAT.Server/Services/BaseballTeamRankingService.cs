using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using KakaoBotAT.Server.Models;

namespace KakaoBotAT.Server.Services;

public partial class BaseballTeamRankingService(IHttpClientFactory httpClientFactory, ILogger<BaseballTeamRankingService> logger) : IBaseballTeamRankingService
{
    private const string BaseballTeamRankingPageAddress = "https://www.koreabaseball.com/Record/TeamRank/TeamRankDaily.aspx";
    private const string BaseballPlayerTopFivePageAddress = "https://www.koreabaseball.com/Record/Ranking/Top5.aspx";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly Lock s_teamRankingCacheLock = new();
    private static readonly Lock s_playerTopFiveCacheLock = new();
    private static readonly Lock s_errorStateLock = new();
    private static BaseballTeamRankingSnapshot? s_cachedTeamRankingSnapshot;
    private static BaseballTopFiveSnapshot? s_cachedPlayerTopFiveSnapshot;
    private static DateTimeOffset s_lastTeamRankingCacheTime = DateTimeOffset.MinValue;
    private static DateTimeOffset s_lastPlayerTopFiveCacheTime = DateTimeOffset.MinValue;
    private static string? s_lastTeamRankingErrorDetails;
    private static string? s_lastPlayerTopFiveErrorDetails;

    private readonly HttpClient _baseballTeamRankingClient = httpClientFactory.CreateClient();

    public async Task<BaseballTeamRankingSnapshot?> GetDailyBaseballTeamRankingSnapshotAsync()
    {
        lock (s_teamRankingCacheLock)
        {
            if (s_cachedTeamRankingSnapshot != null && DateTimeOffset.UtcNow - s_lastTeamRankingCacheTime < CacheDuration)
                return s_cachedTeamRankingSnapshot;
        }

        try
        {
            var pageContent = await FetchPageContentAsync(BaseballTeamRankingPageAddress, "BASEBALL_RANKING");
            if (string.IsNullOrWhiteSpace(pageContent)) return null;

            var baseballTeamRankingSnapshot = ParseBaseballTeamRankingSnapshot(pageContent);
            if (baseballTeamRankingSnapshot == null)
            {
                SetLastTeamRankingErrorDetails(
                    "KBO 팀 순위 페이지 파싱에 실패했습니다.",
                    Environment.StackTrace,
                    $"PageAddress: {BaseballTeamRankingPageAddress}\nPageLength: {pageContent.Length}\nPreviewLines:\n{BuildNormalizedLinePreview(pageContent, 20)}");
                logger.LogError("[BASEBALL_RANKING] Failed to parse ranking page");
                return null;
            }

            lock (s_teamRankingCacheLock)
            {
                s_cachedTeamRankingSnapshot = baseballTeamRankingSnapshot;
                s_lastTeamRankingCacheTime = DateTimeOffset.UtcNow;
            }

            ClearLastTeamRankingErrorDetails();
            return baseballTeamRankingSnapshot;
        }
        catch (Exception exception)
        {
            SetLastTeamRankingErrorDetails(exception.Message, exception.StackTrace, exception.ToString());
            logger.LogError(exception, "[BASEBALL_RANKING] Error fetching baseball team rankings");
            return null;
        }
    }

    public async Task<BaseballTopFiveSnapshot?> GetBaseballTopFiveSnapshotAsync()
    {
        lock (s_playerTopFiveCacheLock)
        {
            if (s_cachedPlayerTopFiveSnapshot != null && DateTimeOffset.UtcNow - s_lastPlayerTopFiveCacheTime < CacheDuration)
                return s_cachedPlayerTopFiveSnapshot;
        }

        try
        {
            var pageContent = await FetchPageContentAsync(BaseballPlayerTopFivePageAddress, "BASEBALL_TOP5");
            if (string.IsNullOrWhiteSpace(pageContent)) return null;

            var baseballTopFiveSnapshot = ParseBaseballTopFiveSnapshot(pageContent);
            if (baseballTopFiveSnapshot == null)
            {
                SetLastPlayerTopFiveErrorDetails(
                    "KBO TOP5 페이지 파싱에 실패했습니다.",
                    Environment.StackTrace,
                    $"PageAddress: {BaseballPlayerTopFivePageAddress}\nPageLength: {pageContent.Length}\nPreviewLines:\n{BuildNormalizedLinePreview(pageContent, 40)}");
                logger.LogError("[BASEBALL_TOP5] Failed to parse top five page");
                return null;
            }

            lock (s_playerTopFiveCacheLock)
            {
                s_cachedPlayerTopFiveSnapshot = baseballTopFiveSnapshot;
                s_lastPlayerTopFiveCacheTime = DateTimeOffset.UtcNow;
            }

            ClearLastPlayerTopFiveErrorDetails();
            return baseballTopFiveSnapshot;
        }
        catch (Exception exception)
        {
            SetLastPlayerTopFiveErrorDetails(exception.Message, exception.StackTrace, exception.ToString());
            logger.LogError(exception, "[BASEBALL_TOP5] Error fetching baseball top five rankings");
            return null;
        }
    }

    public string? GetLastTeamRankingErrorDetails()
    {
        lock (s_errorStateLock)
        {
            return s_lastTeamRankingErrorDetails;
        }
    }

    public string? GetLastPlayerTopFiveErrorDetails()
    {
        lock (s_errorStateLock)
        {
            return s_lastPlayerTopFiveErrorDetails;
        }
    }

    private async Task<string?> FetchPageContentAsync(string pageAddress, string logTag)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, pageAddress);
        requestMessage.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/135.0.0.0 Safari/537.36");
        requestMessage.Headers.Referrer = new Uri("https://www.koreabaseball.com/");

        using var responseMessage = await _baseballTeamRankingClient.SendAsync(requestMessage);
        if (!responseMessage.IsSuccessStatusCode)
        {
            var errorDetails = BuildErrorDetails(
                $"HTTP 요청이 실패했습니다. StatusCode={(int)responseMessage.StatusCode} ({responseMessage.StatusCode})",
                Environment.StackTrace,
                $"PageAddress: {pageAddress}");
            if (logTag.Equals("BASEBALL_RANKING", StringComparison.Ordinal)) SetLastTeamRankingErrorDetails(errorDetails);
            if (logTag.Equals("BASEBALL_TOP5", StringComparison.Ordinal)) SetLastPlayerTopFiveErrorDetails(errorDetails);
            logger.LogError("[{LogTag}] Failed to fetch page with status code {StatusCode}", logTag, responseMessage.StatusCode);
            return null;
        }

        return await responseMessage.Content.ReadAsStringAsync();
    }

    private static BaseballTeamRankingSnapshot? ParseBaseballTeamRankingSnapshot(string pageContent)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(pageContent);

        var rankingDate = ExtractRankingDate(htmlDocument.DocumentNode.InnerText);
        if (rankingDate == null) return null;

        var tableParsedTeamStandings = ParseTableRows(htmlDocument);
        if (tableParsedTeamStandings.Count > 0)
            return new BaseballTeamRankingSnapshot(rankingDate.Value, tableParsedTeamStandings);

        var textParsedTeamStandings = ParseTextRows(WebUtility.HtmlDecode(htmlDocument.DocumentNode.InnerText));
        if (textParsedTeamStandings.Count > 0)
            return new BaseballTeamRankingSnapshot(rankingDate.Value, textParsedTeamStandings);

        return null;
    }

    private static BaseballTopFiveSnapshot? ParseBaseballTopFiveSnapshot(string pageContent)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(pageContent);

        var battingTopFiveStatistics = ParseRequestedTopFiveStatistics(htmlDocument, ["타율", "홈런"]);
        var pitchingTopFiveStatistics = ParseRequestedTopFiveStatistics(htmlDocument, ["평균자책점", "승리"]);

        if (battingTopFiveStatistics.Count == 0 && pitchingTopFiveStatistics.Count == 0) return null;
        return new BaseballTopFiveSnapshot(battingTopFiveStatistics, pitchingTopFiveStatistics);
    }

    private static DateOnly? ExtractRankingDate(string pageText)
    {
        var rankingDateMatch = RankingDateRegex().Match(pageText);
        if (!rankingDateMatch.Success) return null;

        if (!int.TryParse(rankingDateMatch.Groups["Year"].Value, out var year)) return null;
        if (!int.TryParse(rankingDateMatch.Groups["Month"].Value, out var month)) return null;
        if (!int.TryParse(rankingDateMatch.Groups["Day"].Value, out var day)) return null;

        return new DateOnly(year, month, day);
    }

    private static List<BaseballTeamStanding> ParseTableRows(HtmlDocument htmlDocument)
    {
        var teamStandings = new List<BaseballTeamStanding>();
        var tableRows = htmlDocument.DocumentNode.SelectNodes("//tr");
        if (tableRows == null) return teamStandings;

        foreach (var tableRow in tableRows)
        {
            var tableCells = tableRow.SelectNodes("./th|./td");
            if (tableCells == null || tableCells.Count < 12) continue;

            var cellValues = tableCells
                .Select(tableCell => NormalizeCellText(WebUtility.HtmlDecode(tableCell.InnerText)))
                .Where(cellValue => !string.IsNullOrWhiteSpace(cellValue))
                .ToList();

            if (cellValues.Count < 12) continue;

            var parsedTeamStanding = TryCreateTeamStanding(cellValues[0], cellValues[1], cellValues[2], cellValues[3], cellValues[4], cellValues[5], cellValues[6], cellValues[7], cellValues[8], cellValues[9], cellValues[10], cellValues[11]);
            if (parsedTeamStanding != null) teamStandings.Add(parsedTeamStanding);
        }

        return teamStandings;
    }

    private static List<BaseballTeamStanding> ParseTextRows(string pageText)
    {
        var teamStandings = new List<BaseballTeamStanding>();
        var normalizedLines = pageText
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeCellText)
            .Where(normalizedLine => !string.IsNullOrWhiteSpace(normalizedLine));

        foreach (var normalizedLine in normalizedLines)
        {
            var rankingLineMatch = TeamRankingLineRegex().Match(normalizedLine);
            if (!rankingLineMatch.Success) continue;

            var parsedTeamStanding = TryCreateTeamStanding(
                rankingLineMatch.Groups["Rank"].Value,
                rankingLineMatch.Groups["TeamName"].Value,
                rankingLineMatch.Groups["Games"].Value,
                rankingLineMatch.Groups["Wins"].Value,
                rankingLineMatch.Groups["Losses"].Value,
                rankingLineMatch.Groups["Draws"].Value,
                rankingLineMatch.Groups["WinningPercentage"].Value,
                rankingLineMatch.Groups["GamesBehind"].Value,
                rankingLineMatch.Groups["RecentTenGames"].Value,
                rankingLineMatch.Groups["Streak"].Value,
                rankingLineMatch.Groups["HomeRecord"].Value,
                rankingLineMatch.Groups["AwayRecord"].Value);

            if (parsedTeamStanding != null) teamStandings.Add(parsedTeamStanding);
        }

        return teamStandings;
    }

    private static BaseballTeamStanding? TryCreateTeamStanding(
        string rankText,
        string teamNameText,
        string gamesText,
        string winsText,
        string lossesText,
        string drawsText,
        string winningPercentageText,
        string gamesBehindText,
        string recentTenGamesText,
        string streakText,
        string homeRecordText,
        string awayRecordText)
    {
        if (!int.TryParse(rankText, out var rank)) return null;
        if (!int.TryParse(gamesText, out var games)) return null;
        if (!int.TryParse(winsText, out var wins)) return null;
        if (!int.TryParse(lossesText, out var losses)) return null;
        if (!int.TryParse(drawsText, out var draws)) return null;
        if (!decimal.TryParse(winningPercentageText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var winningPercentage)) return null;

        return new BaseballTeamStanding(
            rank,
            teamNameText,
            games,
            wins,
            losses,
            draws,
            winningPercentage,
            gamesBehindText,
            recentTenGamesText,
            streakText,
            homeRecordText,
            awayRecordText);
    }

    private static IReadOnlyList<BaseballTopFiveStatistic> ParseRequestedTopFiveStatistics(
        HtmlDocument htmlDocument,
        IReadOnlyList<string> statisticNames)
    {
        var parsedStatistics = new List<BaseballTopFiveStatistic>();

        foreach (var statisticName in statisticNames)
        {
            var parsedStatistic = TryParseTopFiveStatistic(htmlDocument, statisticName);
            if (parsedStatistic != null) parsedStatistics.Add(parsedStatistic);
        }

        return parsedStatistics;
    }

    private static BaseballTopFiveStatistic? TryParseTopFiveStatistic(HtmlDocument htmlDocument, string statisticName)
    {
        var playerEntries = new List<BaseballPlayerTopFiveEntry>();
        var statisticTitleNode = FindTopFiveStatisticTitleNode(htmlDocument, statisticName);
        if (statisticTitleNode == null) return null;

        var seenPlayerEntries = new HashSet<string>(StringComparer.Ordinal);
        var followingNodes = statisticTitleNode.SelectNodes("following::*");
        if (followingNodes == null) return null;

        foreach (var followingNode in followingNodes)
        {
            var normalizedNodeText = NormalizeHtmlNodeText(followingNode);
            if (string.IsNullOrWhiteSpace(normalizedNodeText)) continue;

            if (IsTopFiveStatisticTitleText(normalizedNodeText))
            {
                if (playerEntries.Count > 0) break;
                continue;
            }

            if (normalizedNodeText.Equals("기록이 없습니다", StringComparison.Ordinal))
                break;

            var parsedPlayerEntry = TryParsePlayerTopFiveEntry(followingNode);
            if (parsedPlayerEntry == null) continue;

            var playerEntryKey = $"{parsedPlayerEntry.Rank}:{parsedPlayerEntry.PlayerName}:{parsedPlayerEntry.TeamName}:{parsedPlayerEntry.StatisticValue}";
            if (!seenPlayerEntries.Add(playerEntryKey)) continue;

            playerEntries.Add(parsedPlayerEntry);
            if (playerEntries.Count == 5) break;
        }

        return playerEntries.Count == 0 ? null : new BaseballTopFiveStatistic(statisticName, playerEntries);
    }

    private static HtmlNode? FindTopFiveStatisticTitleNode(HtmlDocument htmlDocument, string statisticName)
    {
        var statisticTitle = $"{statisticName} TOP5";

        return htmlDocument.DocumentNode
            .Descendants()
            .Where(htmlNode => htmlNode.NodeType == HtmlNodeType.Text)
            .Select(htmlNode => htmlNode.ParentNode)
            .Where(htmlNode => htmlNode != null)
            .Distinct()
            .OrderBy(htmlNode => NormalizeHtmlNodeText(htmlNode!).Length)
            .FirstOrDefault(htmlNode =>
                NormalizeHtmlNodeText(htmlNode!).StartsWith(statisticTitle, StringComparison.Ordinal));
    }

    private static BaseballPlayerTopFiveEntry? TryParsePlayerTopFiveEntry(HtmlNode htmlNode)
    {
        if (!IsPlayerEntryContainerNode(htmlNode)) return null;

        var normalizedNodeText = NormalizeHtmlNodeText(htmlNode);
        if (string.IsNullOrWhiteSpace(normalizedNodeText)) return null;

        var firstSeparatorIndex = normalizedNodeText.IndexOf('.');
        if (firstSeparatorIndex < 1) return null;

        var rankText = normalizedNodeText[..firstSeparatorIndex].Trim();
        if (!int.TryParse(rankText, out var rank)) return null;
        if (rank is < 1 or > 5) return null;

        var remainingText = normalizedNodeText[(firstSeparatorIndex + 1)..].Trim();
        if (string.IsNullOrWhiteSpace(remainingText)) return null;

        var valueTokens = remainingText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (valueTokens.Length < 3) return null;

        var teamTokenIndex = Array.FindIndex(valueTokens, IsKboTeamNameToken);
        if (teamTokenIndex <= 0 || teamTokenIndex >= valueTokens.Length - 1) return null;

        var playerName = string.Join(' ', valueTokens[..teamTokenIndex]);
        var teamName = valueTokens[teamTokenIndex];
        var statisticValue = string.Join(' ', valueTokens[(teamTokenIndex + 1)..]);

        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(statisticValue)) return null;

        return new BaseballPlayerTopFiveEntry(rank, playerName, teamName, statisticValue);
    }

    private static bool IsPlayerEntryContainerNode(HtmlNode htmlNode)
    {
        return htmlNode.Name.Equals("li", StringComparison.OrdinalIgnoreCase) ||
               htmlNode.Name.Equals("tr", StringComparison.OrdinalIgnoreCase) ||
               htmlNode.Name.Equals("dd", StringComparison.OrdinalIgnoreCase) ||
               htmlNode.Name.Equals("p", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTopFiveStatisticTitleText(string normalizedNodeText)
    {
        if (!normalizedNodeText.EndsWith("TOP5", StringComparison.Ordinal)) return false;

        return normalizedNodeText.StartsWith("타율 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("홈런 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("타점 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("도루 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("득점 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("안타 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("출루율 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("장타율 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("2루타 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("3루타 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("루타 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("OPS ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("타수 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("볼넷 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("삼진 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("평균자책점 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("승리 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("세이브 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("승률 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("홀드 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("탈삼진 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("경기 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("패배 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("이닝 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("WHIP ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("완투 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("완봉 ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("QS ", StringComparison.Ordinal) ||
               normalizedNodeText.StartsWith("피안타율 ", StringComparison.Ordinal);
    }

    private static bool IsKboTeamNameToken(string token)
    {
        return token.Equals("KT", StringComparison.Ordinal) ||
               token.Equals("LG", StringComparison.Ordinal) ||
               token.Equals("SSG", StringComparison.Ordinal) ||
               token.Equals("삼성", StringComparison.Ordinal) ||
               token.Equals("KIA", StringComparison.Ordinal) ||
               token.Equals("한화", StringComparison.Ordinal) ||
               token.Equals("NC", StringComparison.Ordinal) ||
               token.Equals("두산", StringComparison.Ordinal) ||
               token.Equals("롯데", StringComparison.Ordinal) ||
               token.Equals("키움", StringComparison.Ordinal);
    }

    private static string NormalizeHtmlNodeText(HtmlNode htmlNode) =>
        NormalizeCellText(WebUtility.HtmlDecode(htmlNode.InnerText));

    private static void ClearLastTeamRankingErrorDetails()
    {
        lock (s_errorStateLock)
        {
            s_lastTeamRankingErrorDetails = null;
        }
    }

    private static void ClearLastPlayerTopFiveErrorDetails()
    {
        lock (s_errorStateLock)
        {
            s_lastPlayerTopFiveErrorDetails = null;
        }
    }

    private static void SetLastTeamRankingErrorDetails(string message, string? stackTrace, string? additionalInformation)
    {
        var errorDetails = BuildErrorDetails(message, stackTrace, additionalInformation);
        SetLastTeamRankingErrorDetails(errorDetails);
    }

    private static void SetLastTeamRankingErrorDetails(string errorDetails)
    {
        lock (s_errorStateLock)
        {
            s_lastTeamRankingErrorDetails = errorDetails;
        }
    }

    private static void SetLastPlayerTopFiveErrorDetails(string message, string? stackTrace, string? additionalInformation)
    {
        var errorDetails = BuildErrorDetails(message, stackTrace, additionalInformation);
        SetLastPlayerTopFiveErrorDetails(errorDetails);
    }

    private static void SetLastPlayerTopFiveErrorDetails(string errorDetails)
    {
        lock (s_errorStateLock)
        {
            s_lastPlayerTopFiveErrorDetails = errorDetails;
        }
    }

    private static string BuildErrorDetails(string message, string? stackTrace, string? additionalInformation)
    {
        var errorMessage = string.IsNullOrWhiteSpace(message) ? "알 수 없는 오류" : message;
        var errorStackTrace = string.IsNullOrWhiteSpace(stackTrace) ? "스택 추적 정보 없음" : stackTrace;
        var errorAdditionalInformation = string.IsNullOrWhiteSpace(additionalInformation) ? "추가 정보 없음" : additionalInformation;
        return $"Message: {errorMessage}\nStackTrace: {errorStackTrace}\nAdditionalInfo: {errorAdditionalInformation}";
    }

    private static string BuildNormalizedLinePreview(string pageContent, int maximumLineCount)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(pageContent);

        return string.Join(
            "\n",
            htmlDocument.DocumentNode.InnerText
                .Replace("\r", string.Empty)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(pageLine => NormalizeCellText(WebUtility.HtmlDecode(pageLine)))
                .Where(pageLine => !string.IsNullOrWhiteSpace(pageLine))
                .Take(maximumLineCount));
    }

    private static string NormalizeCellText(string value) => WhiteSpaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(@"(?<Year>\d{4})\.(?<Month>\d{2})\.(?<Day>\d{2})")]
    private static partial Regex RankingDateRegex();

    [GeneratedRegex(@"^(?<Rank>\d+)\s+(?<TeamName>KT|LG|SSG|삼성|KIA|한화|NC|두산|롯데|키움)\s+(?<Games>\d+)\s+(?<Wins>\d+)\s+(?<Losses>\d+)\s+(?<Draws>\d+)\s+(?<WinningPercentage>\d+\.\d+)\s+(?<GamesBehind>[-\d.]+)\s+(?<RecentTenGames>\S+)\s+(?<Streak>\S+)\s+(?<HomeRecord>\d+-\d+-\d+)\s+(?<AwayRecord>\d+-\d+-\d+)$")]
    private static partial Regex TeamRankingLineRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}
