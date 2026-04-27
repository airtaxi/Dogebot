using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public partial class BaseballTeamRankingService(IHttpClientFactory httpClientFactory, ILogger<BaseballTeamRankingService> logger) : IBaseballTeamRankingService
{
    private const string BaseballTeamRankingPageAddress = "https://www.koreabaseball.com/Record/TeamRank/TeamRankDaily.aspx";
    private const string BaseballPlayerTopFivePageAddress = "https://www.koreabaseball.com/Record/Ranking/Top5.aspx";
    private const string BaseballCrowdRankingPageAddress = "https://www.koreabaseball.com/ws/Record.asmx/GetCrowdTeam";
    private const string BaseballNewsPageAddress = "https://www.koreabaseball.com/MediaNews/News/BreakingNews/List.aspx";
    private const string BaseballCrowdReferrerPageAddress = "https://www.koreabaseball.com/Record/Crowd/GraphTeam.aspx";
    private const string BaseballHostHeaderValue = "www.koreabaseball.com";
    private const string BaseballUserAgentValue = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 Edg/148.0.0.0";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly Lock s_teamRankingCacheLock = new();
    private static readonly Lock s_playerTopFiveCacheLock = new();
    private static readonly Lock s_crowdRankingCacheLock = new();
    private static readonly Lock s_newsCacheLock = new();
    private static readonly Lock s_errorStateLock = new();
    private static BaseballTeamRankingSnapshot? s_cachedTeamRankingSnapshot;
    private static BaseballTopFiveSnapshot? s_cachedPlayerTopFiveSnapshot;
    private static BaseballCrowdRankingSnapshot? s_cachedCrowdRankingSnapshot;
    private static BaseballNewsSnapshot? s_cachedNewsSnapshot;
    private static DateTimeOffset s_lastTeamRankingCacheTime = DateTimeOffset.MinValue;
    private static DateTimeOffset s_lastPlayerTopFiveCacheTime = DateTimeOffset.MinValue;
    private static DateTimeOffset s_lastCrowdRankingCacheTime = DateTimeOffset.MinValue;
    private static DateTimeOffset s_lastNewsCacheTime = DateTimeOffset.MinValue;
    private static string? s_lastTeamRankingErrorDetails;
    private static string? s_lastPlayerTopFiveErrorDetails;
    private static string? s_lastCrowdRankingErrorDetails;
    private static string? s_lastNewsErrorDetails;

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
                    $"PageAddress: {BaseballPlayerTopFivePageAddress}\nPageLength: {pageContent.Length}\nPreviewLines:\n{BuildNormalizedLinePreview(pageContent, 40, ["타율 TOP5", "홈런 TOP5", "평균자책점 TOP5", "승리 TOP5"])}");
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

    public async Task<BaseballCrowdRankingSnapshot?> GetBaseballCrowdRankingSnapshotAsync()
    {
        lock (s_crowdRankingCacheLock)
        {
            if (s_cachedCrowdRankingSnapshot != null && DateTimeOffset.UtcNow - s_lastCrowdRankingCacheTime < CacheDuration)
                return s_cachedCrowdRankingSnapshot;
        }

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, BaseballCrowdRankingPageAddress)
            {
                Content = new StringContent("leagueId=1&seriesId=0&gameMonth=0", Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            requestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded; charset=UTF-8");
            ConfigureBaseballCrowdRequestHeaders(requestMessage);

            using var responseMessage = await _baseballTeamRankingClient.SendAsync(requestMessage);
            var responseContent = await responseMessage.Content.ReadAsStringAsync();
            if (!responseMessage.IsSuccessStatusCode)
            {
                SetLastCrowdRankingErrorDetails(
                    $"HTTP 요청이 실패했습니다. StatusCode={(int)responseMessage.StatusCode} ({responseMessage.StatusCode})",
                    Environment.StackTrace,
                    $"PageAddress: {BaseballCrowdRankingPageAddress}\nResponsePreview:\n{BuildContentPreview(responseContent, 1000)}");
                logger.LogError("[BASEBALL_CROWD] Failed to fetch crowd page with status code {StatusCode}", responseMessage.StatusCode);
                return null;
            }

            if (string.IsNullOrWhiteSpace(responseContent))
            {
                SetLastCrowdRankingErrorDetails(
                    "KBO 관중 순위 응답이 비어 있습니다.",
                    Environment.StackTrace,
                    $"PageAddress: {BaseballCrowdRankingPageAddress}");
                logger.LogError("[BASEBALL_CROWD] Crowd response content was empty");
                return null;
            }

            var baseballCrowdRankingSnapshot = ParseBaseballCrowdRankingSnapshot(responseContent);
            if (baseballCrowdRankingSnapshot == null)
            {
                SetLastCrowdRankingErrorDetails(
                    "KBO 관중 순위 응답 파싱에 실패했습니다.",
                    Environment.StackTrace,
                    $"PageAddress: {BaseballCrowdRankingPageAddress}\nResponsePreview:\n{BuildContentPreview(responseContent, 1000)}");
                logger.LogError("[BASEBALL_CROWD] Failed to parse crowd response");
                return null;
            }

            lock (s_crowdRankingCacheLock)
            {
                s_cachedCrowdRankingSnapshot = baseballCrowdRankingSnapshot;
                s_lastCrowdRankingCacheTime = DateTimeOffset.UtcNow;
            }

            ClearLastCrowdRankingErrorDetails();
            return baseballCrowdRankingSnapshot;
        }
        catch (Exception exception)
        {
            SetLastCrowdRankingErrorDetails(exception.Message, exception.StackTrace, exception.ToString());
            logger.LogError(exception, "[BASEBALL_CROWD] Error fetching baseball crowd rankings");
            return null;
        }
    }

    public async Task<BaseballNewsSnapshot?> GetBaseballNewsSnapshotAsync()
    {
        var targetDate = GetBaseballNewsTargetDate();

        lock (s_newsCacheLock)
        {
            if (s_cachedNewsSnapshot != null &&
                s_cachedNewsSnapshot.TargetDate == targetDate &&
                DateTimeOffset.UtcNow - s_lastNewsCacheTime < CacheDuration)
                return s_cachedNewsSnapshot;
        }

        try
        {
            var pageContent = await FetchPageContentAsync(BaseballNewsPageAddress, "BASEBALL_NEWS");
            if (string.IsNullOrWhiteSpace(pageContent)) return null;

            var baseballNewsSnapshot = ParseBaseballNewsSnapshot(pageContent, targetDate);
            if (baseballNewsSnapshot == null)
            {
                var targetDateText = targetDate.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
                SetLastNewsErrorDetails(
                    "KBO 뉴스 페이지 파싱에 실패했습니다.",
                    Environment.StackTrace,
                    $"PageAddress: {BaseballNewsPageAddress}\nPageLength: {pageContent.Length}\nPreviewLines:\n{BuildNormalizedLinePreview(pageContent, 40, [targetDateText])}");
                logger.LogError("[BASEBALL_NEWS] Failed to parse news page");
                return null;
            }

            lock (s_newsCacheLock)
            {
                s_cachedNewsSnapshot = baseballNewsSnapshot;
                s_lastNewsCacheTime = DateTimeOffset.UtcNow;
            }

            ClearLastNewsErrorDetails();
            return baseballNewsSnapshot;
        }
        catch (Exception exception)
        {
            SetLastNewsErrorDetails(exception.Message, exception.StackTrace, exception.ToString());
            logger.LogError(exception, "[BASEBALL_NEWS] Error fetching baseball news");
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

    public string? GetLastCrowdRankingErrorDetails()
    {
        lock (s_errorStateLock)
        {
            return s_lastCrowdRankingErrorDetails;
        }
    }

    public string? GetLastNewsErrorDetails()
    {
        lock (s_errorStateLock)
        {
            return s_lastNewsErrorDetails;
        }
    }

    private async Task<string?> FetchPageContentAsync(string pageAddress, string logTag)
    {
        using var requestMessage = new HttpRequestMessage(HttpMethod.Get, pageAddress);
        ConfigureBaseballRequestHeaders(requestMessage);

        using var responseMessage = await _baseballTeamRankingClient.SendAsync(requestMessage);
        if (!responseMessage.IsSuccessStatusCode)
        {
            var errorDetails = BuildErrorDetails(
                $"HTTP 요청이 실패했습니다. StatusCode={(int)responseMessage.StatusCode} ({responseMessage.StatusCode})",
                Environment.StackTrace,
                $"PageAddress: {pageAddress}");
            if (logTag.Equals("BASEBALL_RANKING", StringComparison.Ordinal)) SetLastTeamRankingErrorDetails(errorDetails);
            if (logTag.Equals("BASEBALL_TOP5", StringComparison.Ordinal)) SetLastPlayerTopFiveErrorDetails(errorDetails);
            if (logTag.Equals("BASEBALL_NEWS", StringComparison.Ordinal)) SetLastNewsErrorDetails(errorDetails);
            logger.LogError("[{LogTag}] Failed to fetch page with status code {StatusCode}", logTag, responseMessage.StatusCode);
            return null;
        }

        return await responseMessage.Content.ReadAsStringAsync();
    }

    private static void ConfigureBaseballRequestHeaders(HttpRequestMessage requestMessage)
    {
        requestMessage.Version = HttpVersion.Version11;
        requestMessage.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        requestMessage.Headers.Host = BaseballHostHeaderValue;
        requestMessage.Headers.Referrer = new Uri("https://www.koreabaseball.com/");
        requestMessage.Headers.Accept.Clear();
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        requestMessage.Headers.Connection.Clear();
        requestMessage.Headers.Connection.Add("keep-alive");
        requestMessage.Headers.UserAgent.Clear();
        requestMessage.Headers.UserAgent.ParseAdd(BaseballUserAgentValue);
    }

    private static void ConfigureBaseballCrowdRequestHeaders(HttpRequestMessage requestMessage)
    {
        ConfigureBaseballRequestHeaders(requestMessage);
        requestMessage.Headers.Referrer = new Uri(BaseballCrowdReferrerPageAddress);
        requestMessage.Headers.Accept.Clear();
        requestMessage.Headers.TryAddWithoutValidation("accept", "application/json, text/javascript, */*; q=0.01");
        requestMessage.Headers.TryAddWithoutValidation("accept-language", "en-US,en;q=0.9,ko;q=0.8");
        requestMessage.Headers.TryAddWithoutValidation("priority", "u=1, i");
        requestMessage.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"148\", \"Microsoft Edge\";v=\"148\", \"Not/A)Brand\";v=\"99\"");
        requestMessage.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        requestMessage.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        requestMessage.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
        requestMessage.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
        requestMessage.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
        requestMessage.Headers.TryAddWithoutValidation("x-requested-with", "XMLHttpRequest");
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

    private static BaseballCrowdRankingSnapshot? ParseBaseballCrowdRankingSnapshot(string responseContent)
    {
        var baseballCrowdResponse = JsonSerializer.Deserialize<BaseballCrowdRankingResponsePayload>(responseContent);
        if (baseballCrowdResponse == null) return null;
        if (!baseballCrowdResponse.ResultCode.Equals("100", StringComparison.Ordinal)) return null;
        if (string.IsNullOrWhiteSpace(baseballCrowdResponse.Categories)) return null;
        if (string.IsNullOrWhiteSpace(baseballCrowdResponse.DateText)) return null;

        var teamNames = baseballCrowdResponse.Categories
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();
        if (teamNames.Count == 0) return null;

        var crowdDataSeries = baseballCrowdResponse.DataSeries?.FirstOrDefault(dataSeries =>
            dataSeries.CrowdCounts != null && dataSeries.CrowdCounts.Count > 0);
        if (crowdDataSeries?.CrowdCounts == null) return null;
        if (teamNames.Count != crowdDataSeries.CrowdCounts.Count) return null;

        var crowdRankings = teamNames
            .Select((teamName, index) => new { TeamName = teamName, CrowdCount = crowdDataSeries.CrowdCounts[index] })
            .OrderByDescending(crowdRanking => crowdRanking.CrowdCount)
            .ThenBy(crowdRanking => crowdRanking.TeamName, StringComparer.Ordinal)
            .Select((crowdRanking, index) => new BaseballCrowdRankingEntry(index + 1, crowdRanking.TeamName, crowdRanking.CrowdCount))
            .ToList();

        return crowdRankings.Count == 0 ? null : new BaseballCrowdRankingSnapshot(baseballCrowdResponse.DateText, crowdRankings);
    }

    private static BaseballNewsSnapshot? ParseBaseballNewsSnapshot(string pageContent, DateOnly targetDate)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(pageContent);

        var newsListItemNodes = htmlDocument.DocumentNode.SelectNodes("//ul[contains(concat(' ', normalize-space(@class), ' '), ' boardPhoto ')]/li");
        if (newsListItemNodes == null || newsListItemNodes.Count == 0) return null;

        var parsedNewsItems = new List<BaseballNewsItem>();
        foreach (var newsListItemNode in newsListItemNodes)
        {
            var parsedNewsItem = TryParseBaseballNewsItem(newsListItemNode);
            if (parsedNewsItem != null) parsedNewsItems.Add(parsedNewsItem);
        }

        if (parsedNewsItems.Count == 0) return null;

        var filteredNewsItems = parsedNewsItems
            .Where(newsItem => newsItem.PublishedDate == targetDate)
            .ToList();

        return new BaseballNewsSnapshot(targetDate, filteredNewsItems);
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

    private static BaseballNewsItem? TryParseBaseballNewsItem(HtmlNode newsListItemNode)
    {
        var titleNode = newsListItemNode.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' txt ')]//strong/a");
        var summaryParagraphNode = newsListItemNode.SelectSingleNode(".//div[contains(concat(' ', normalize-space(@class), ' '), ' txt ')]/p");
        var dateNode = newsListItemNode.SelectSingleNode(".//span[contains(concat(' ', normalize-space(@class), ' '), ' date ')]");
        if (titleNode == null || summaryParagraphNode == null || dateNode == null) return null;

        var title = NormalizeHtmlNodeText(titleNode);
        var publishedDateText = NormalizeHtmlNodeText(dateNode);
        if (!DateOnly.TryParseExact(publishedDateText, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var publishedDate))
            return null;

        var summary = BuildBaseballNewsSummary(summaryParagraphNode);
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(summary)) return null;

        return new BaseballNewsItem(publishedDate, title, summary);
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
        var topFiveStatisticContainerNodes = FindTopFiveStatisticContainerNodes(htmlDocument);

        foreach (var statisticName in statisticNames)
        {
            var parsedStatistic = TryParseTopFiveStatistic(topFiveStatisticContainerNodes, statisticName);
            if (parsedStatistic != null) parsedStatistics.Add(parsedStatistic);
        }

        return parsedStatistics;
    }

    private static IReadOnlyList<HtmlNode> FindTopFiveStatisticContainerNodes(HtmlDocument htmlDocument)
    {
        var statisticContainerNodes = htmlDocument.DocumentNode
            .Descendants("div")
            .Where(htmlNode => HasCssClass(htmlNode, "record"))
            .ToList();

        return statisticContainerNodes;
    }

    private static BaseballTopFiveStatistic? TryParseTopFiveStatistic(
        IReadOnlyList<HtmlNode> topFiveStatisticContainerNodes,
        string statisticName)
    {
        var statisticTitle = $"{statisticName} TOP5";
        var statisticContainerNode = topFiveStatisticContainerNodes.FirstOrDefault(htmlNode =>
        {
            var titleNode = htmlNode.Descendants("span").FirstOrDefault(titleHtmlNode => HasCssClass(titleHtmlNode, "title"));
            if (titleNode == null) return false;

            return NormalizeHtmlNodeText(titleNode).Equals(statisticTitle, StringComparison.Ordinal);
        });
        if (statisticContainerNode == null) return null;

        var rankingListItemNodes = statisticContainerNode.SelectNodes(".//ol[contains(concat(' ', normalize-space(@class), ' '), ' rankList ')]/li");
        if (rankingListItemNodes == null || rankingListItemNodes.Count == 0) return null;

        var playerEntries = new List<BaseballPlayerTopFiveEntry>();
        foreach (var rankingListItemNode in rankingListItemNodes)
        {
            var fallbackRank = playerEntries.Count + 1;
            var parsedPlayerEntry = TryParsePlayerTopFiveEntry(rankingListItemNode, fallbackRank);
            if (parsedPlayerEntry == null) continue;
            playerEntries.Add(parsedPlayerEntry);
        }

        return playerEntries.Count == 0 ? null : new BaseballTopFiveStatistic(statisticName, playerEntries);
    }

    private static BaseballPlayerTopFiveEntry? TryParsePlayerTopFiveEntry(HtmlNode rankingListItemNode, int fallbackRank)
    {
        var nameNode = rankingListItemNode.Descendants("span").FirstOrDefault(htmlNode => HasCssClass(htmlNode, "name"));
        var teamNode = rankingListItemNode.Descendants("span").FirstOrDefault(htmlNode => HasCssClass(htmlNode, "team"));
        var statisticValueNode = rankingListItemNode.Descendants("span").FirstOrDefault(htmlNode => HasCssClass(htmlNode, "rr"));
        if (nameNode == null || teamNode == null || statisticValueNode == null) return null;

        var rank = ExtractRankFromClassAttribute(nameNode.GetAttributeValue("class", string.Empty), fallbackRank);
        if (rank is < 1 or > 5) return null;

        var playerName = NormalizeHtmlNodeText(nameNode);
        var teamName = NormalizeHtmlNodeText(teamNode);
        var statisticValue = NormalizeHtmlNodeText(statisticValueNode);
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(teamName) || string.IsNullOrWhiteSpace(statisticValue))
            return null;

        return new BaseballPlayerTopFiveEntry(rank, playerName, teamName, statisticValue);
    }

    private static int ExtractRankFromClassAttribute(string classAttributeValue, int fallbackRank)
    {
        var classTokens = classAttributeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var classToken in classTokens)
        {
            if (!classToken.StartsWith("rank", StringComparison.Ordinal)) continue;

            var rankText = classToken["rank".Length..];
            if (int.TryParse(rankText, out var parsedRank)) return parsedRank;
        }

        return fallbackRank;
    }

    private static bool HasCssClass(HtmlNode htmlNode, string className)
    {
        var classAttributeValue = htmlNode.GetAttributeValue("class", string.Empty);
        if (string.IsNullOrWhiteSpace(classAttributeValue)) return false;

        var classTokens = classAttributeValue.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return classTokens.Contains(className, StringComparer.Ordinal);
    }

    private static string BuildBaseballNewsSummary(HtmlNode summaryParagraphNode)
    {
        var summarySegments = summaryParagraphNode.ChildNodes
            .Where(htmlNode => !(htmlNode.Name.Equals("span", StringComparison.OrdinalIgnoreCase) && HasCssClass(htmlNode, "date")))
            .Select(htmlNode => NormalizeCellText(WebUtility.HtmlDecode(htmlNode.InnerText)))
            .Where(summarySegment => !string.IsNullOrWhiteSpace(summarySegment))
            .ToList();

        return string.Join(" ", summarySegments);
    }

    private static DateOnly GetBaseballNewsTargetDate()
    {
        var koreanStandardTimeZoneInfo = GetKoreanStandardTimeZoneInfo();
        var currentKoreanDateTimeOffset = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, koreanStandardTimeZoneInfo);
        var targetDate = DateOnly.FromDateTime(currentKoreanDateTimeOffset.DateTime);
        if (currentKoreanDateTimeOffset.Hour < 6) return targetDate.AddDays(-1);

        return targetDate;
    }

    private static TimeZoneInfo GetKoreanStandardTimeZoneInfo()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Seoul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time"); }
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

    private static void ClearLastCrowdRankingErrorDetails()
    {
        lock (s_errorStateLock)
        {
            s_lastCrowdRankingErrorDetails = null;
        }
    }

    private static void ClearLastNewsErrorDetails()
    {
        lock (s_errorStateLock)
        {
            s_lastNewsErrorDetails = null;
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

    private static void SetLastCrowdRankingErrorDetails(string message, string? stackTrace, string? additionalInformation)
    {
        var errorDetails = BuildErrorDetails(message, stackTrace, additionalInformation);
        SetLastCrowdRankingErrorDetails(errorDetails);
    }

    private static void SetLastCrowdRankingErrorDetails(string errorDetails)
    {
        lock (s_errorStateLock)
        {
            s_lastCrowdRankingErrorDetails = errorDetails;
        }
    }

    private static void SetLastNewsErrorDetails(string message, string? stackTrace, string? additionalInformation)
    {
        var errorDetails = BuildErrorDetails(message, stackTrace, additionalInformation);
        SetLastNewsErrorDetails(errorDetails);
    }

    private static void SetLastNewsErrorDetails(string errorDetails)
    {
        lock (s_errorStateLock)
        {
            s_lastNewsErrorDetails = errorDetails;
        }
    }

    private static string BuildErrorDetails(string message, string? stackTrace, string? additionalInformation)
    {
        var errorMessage = string.IsNullOrWhiteSpace(message) ? "알 수 없는 오류" : message;
        var errorStackTrace = string.IsNullOrWhiteSpace(stackTrace) ? "스택 추적 정보 없음" : stackTrace;
        var errorAdditionalInformation = string.IsNullOrWhiteSpace(additionalInformation) ? "추가 정보 없음" : additionalInformation;
        return $"Message: {errorMessage}\nStackTrace: {errorStackTrace}\nAdditionalInfo: {errorAdditionalInformation}";
    }

    private static string BuildNormalizedLinePreview(string pageContent, int maximumLineCount, IReadOnlyList<string>? preferredKeywords = null)
    {
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(pageContent);

        var normalizedLines = htmlDocument.DocumentNode.InnerText
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(pageLine => NormalizeCellText(WebUtility.HtmlDecode(pageLine)))
            .Where(pageLine => !string.IsNullOrWhiteSpace(pageLine))
            .ToList();

        var previewStartIndex = 0;
        if (preferredKeywords != null && preferredKeywords.Count > 0)
        {
            var matchingLineIndex = normalizedLines.FindIndex(pageLine =>
                preferredKeywords.Any(preferredKeyword => pageLine.Contains(preferredKeyword, StringComparison.Ordinal)));
            if (matchingLineIndex >= 0) previewStartIndex = Math.Max(0, matchingLineIndex - 2);
        }

        return string.Join("\n", normalizedLines.Skip(previewStartIndex).Take(maximumLineCount));
    }

    private static string BuildContentPreview(string content, int maximumLength) =>
        content.Length <= maximumLength ? content : $"{content[..maximumLength]}...";

    private static string NormalizeCellText(string value) => WhiteSpaceRegex().Replace(value, " ").Trim();

    private sealed record BaseballCrowdRankingResponsePayload(
        [property: JsonPropertyName("result_cd")] string ResultCode,
        [property: JsonPropertyName("result_msg")] string ResultMessage,
        [property: JsonPropertyName("data")] IReadOnlyList<BaseballCrowdDataSeriesPayload>? DataSeries,
        [property: JsonPropertyName("categories")] string Categories,
        [property: JsonPropertyName("date")] string DateText);

    private sealed record BaseballCrowdDataSeriesPayload(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("data")] IReadOnlyList<int>? CrowdCounts);

    [GeneratedRegex(@"(?<Year>\d{4})\.(?<Month>\d{2})\.(?<Day>\d{2})")]
    private static partial Regex RankingDateRegex();

    [GeneratedRegex(@"^(?<Rank>\d+)\s+(?<TeamName>KT|LG|SSG|삼성|KIA|한화|NC|두산|롯데|키움)\s+(?<Games>\d+)\s+(?<Wins>\d+)\s+(?<Losses>\d+)\s+(?<Draws>\d+)\s+(?<WinningPercentage>\d+\.\d+)\s+(?<GamesBehind>[-\d.]+)\s+(?<RecentTenGames>\S+)\s+(?<Streak>\S+)\s+(?<HomeRecord>\d+-\d+-\d+)\s+(?<AwayRecord>\d+-\d+-\d+)$")]
    private static partial Regex TeamRankingLineRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}

