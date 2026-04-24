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
    private static BaseballTeamRankingSnapshot? s_cachedTeamRankingSnapshot;
    private static BaseballTopFiveSnapshot? s_cachedPlayerTopFiveSnapshot;
    private static DateTimeOffset s_lastTeamRankingCacheTime = DateTimeOffset.MinValue;
    private static DateTimeOffset s_lastPlayerTopFiveCacheTime = DateTimeOffset.MinValue;

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
                logger.LogError("[BASEBALL_RANKING] Failed to parse ranking page");
                return null;
            }

            lock (s_teamRankingCacheLock)
            {
                s_cachedTeamRankingSnapshot = baseballTeamRankingSnapshot;
                s_lastTeamRankingCacheTime = DateTimeOffset.UtcNow;
            }

            return baseballTeamRankingSnapshot;
        }
        catch (Exception exception)
        {
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
                logger.LogError("[BASEBALL_TOP5] Failed to parse top five page");
                return null;
            }

            lock (s_playerTopFiveCacheLock)
            {
                s_cachedPlayerTopFiveSnapshot = baseballTopFiveSnapshot;
                s_lastPlayerTopFiveCacheTime = DateTimeOffset.UtcNow;
            }

            return baseballTopFiveSnapshot;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[BASEBALL_TOP5] Error fetching baseball top five rankings");
            return null;
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

        var normalizedLines = htmlDocument.DocumentNode.InnerText
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(pageLine => NormalizeCellText(WebUtility.HtmlDecode(pageLine)))
            .Where(pageLine => !string.IsNullOrWhiteSpace(pageLine))
            .ToList();

        var battingTopFiveStatistics = ParseRequestedTopFiveStatistics(normalizedLines, ["타율", "홈런"]);
        var pitchingTopFiveStatistics = ParseRequestedTopFiveStatistics(normalizedLines, ["평균자책점", "승리"]);

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

    private static IReadOnlyList<BaseballTopFiveStatistic> ParseRequestedTopFiveStatistics(IReadOnlyList<string> normalizedLines, IReadOnlyList<string> statisticNames)
    {
        var parsedStatistics = new List<BaseballTopFiveStatistic>();

        foreach (var statisticName in statisticNames)
        {
            var parsedStatistic = TryParseTopFiveStatistic(normalizedLines, statisticName);
            if (parsedStatistic != null) parsedStatistics.Add(parsedStatistic);
        }

        return parsedStatistics;
    }

    private static BaseballTopFiveStatistic? TryParseTopFiveStatistic(IReadOnlyList<string> normalizedLines, string statisticName)
    {
        var statisticTitle = $"{statisticName} TOP5";
        var statisticLineIndex = normalizedLines
            .Select((lineText, lineIndex) => new { lineText, lineIndex })
            .FirstOrDefault(indexedLine => indexedLine.lineText.StartsWith(statisticTitle, StringComparison.Ordinal))?
            .lineIndex;

        if (statisticLineIndex == null) return null;

        var playerEntries = new List<BaseballPlayerTopFiveEntry>();

        for (var lineIndex = statisticLineIndex.Value + 1; lineIndex < normalizedLines.Count; lineIndex++)
        {
            var currentLine = normalizedLines[lineIndex];
            if (currentLine.EndsWith("TOP5", StringComparison.Ordinal) && playerEntries.Count > 0) break;
            if (currentLine.Equals("기록이 없습니다", StringComparison.Ordinal)) break;

            var parsedPlayerEntry = TryParsePlayerTopFiveEntry(currentLine);
            if (parsedPlayerEntry != null) playerEntries.Add(parsedPlayerEntry);

            if (playerEntries.Count == 5) break;
        }

        return playerEntries.Count == 0 ? null : new BaseballTopFiveStatistic(statisticName, playerEntries);
    }

    private static BaseballPlayerTopFiveEntry? TryParsePlayerTopFiveEntry(string lineText)
    {
        var playerEntryMatch = PlayerTopFiveEntryRegex().Match(lineText);
        if (!playerEntryMatch.Success) return null;

        if (!int.TryParse(playerEntryMatch.Groups["Rank"].Value, out var rank)) return null;

        return new BaseballPlayerTopFiveEntry(
            rank,
            playerEntryMatch.Groups["PlayerName"].Value,
            playerEntryMatch.Groups["TeamName"].Value,
            playerEntryMatch.Groups["StatisticValue"].Value);
    }

    private static string NormalizeCellText(string value) => WhiteSpaceRegex().Replace(value, " ").Trim();

    [GeneratedRegex(@"(?<Year>\d{4})\.(?<Month>\d{2})\.(?<Day>\d{2})")]
    private static partial Regex RankingDateRegex();

    [GeneratedRegex(@"^(?<Rank>\d+)\s+(?<TeamName>KT|LG|SSG|삼성|KIA|한화|NC|두산|롯데|키움)\s+(?<Games>\d+)\s+(?<Wins>\d+)\s+(?<Losses>\d+)\s+(?<Draws>\d+)\s+(?<WinningPercentage>\d+\.\d+)\s+(?<GamesBehind>[-\d.]+)\s+(?<RecentTenGames>\S+)\s+(?<Streak>\S+)\s+(?<HomeRecord>\d+-\d+-\d+)\s+(?<AwayRecord>\d+-\d+-\d+)$")]
    private static partial Regex TeamRankingLineRegex();

    [GeneratedRegex(@"^(?<Rank>[1-5])\.\s+(?<PlayerName>.+?)\s+(?<TeamName>KT|LG|SSG|삼성|KIA|한화|NC|두산|롯데|키움)\s+(?<StatisticValue>.+)$")]
    private static partial Regex PlayerTopFiveEntryRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}
