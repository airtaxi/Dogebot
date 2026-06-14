using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dogebot.Server.Services;

public class StockService(IHttpClientFactory httpClientFactory, ILogger<StockService> logger) : IStockService
{
    private const string FrontServiceBaseAddress = "https://m.stock.naver.com/front-api";
    private const string ForeignServiceBaseAddress = "https://api.stock.naver.com";
    private const string PollingServiceBaseAddress = "https://polling.finance.naver.com";
    private const string MobileStockBaseAddress = "https://m.stock.naver.com";
    private const string UserAgentHeaderValue = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 Edg/148.0.0.0";
    private const string StockInformationUnavailableMessage = "주식 정보를 가져오지 못했습니다.\n잠시 후 다시 시도해주세요.";
    private const int DisplayLimit = 5;

    private readonly HttpClient _stockClient = httpClientFactory.CreateClient();

    public async Task<string> CreateSummaryMessageAsync(string queryText)
    {
        var resolutionResult = await ResolveStockAsync(queryText, "!주식");
        if (resolutionResult.Message != null) return resolutionResult.Message;

        var resolvedStock = resolutionResult.Stock!;
        var basicStockNode = await FetchBasicStockNodeAsync(resolvedStock);
        if (basicStockNode == null) return StockInformationUnavailableMessage;

        var realtimeStockNode = await FetchRealtimeStockNodeAsync(resolvedStock);
        return FormatSummaryMessage(resolvedStock, basicStockNode, realtimeStockNode);
    }

    public async Task<string> CreateDetailMessageAsync(string queryText)
    {
        var resolutionResult = await ResolveStockAsync(queryText, "!주식상세");
        if (resolutionResult.Message != null) return resolutionResult.Message;

        var resolvedStock = resolutionResult.Stock!;
        var basicStockNode = await FetchBasicStockNodeAsync(resolvedStock);
        if (basicStockNode == null) return StockInformationUnavailableMessage;

        var integrationStockNode = await FetchIntegrationStockNodeAsync(resolvedStock);
        var trendNode = resolvedStock.IsDomestic ? await FetchDomesticTrendNodeAsync(resolvedStock) : null;
        return FormatDetailMessage(resolvedStock, basicStockNode, integrationStockNode, trendNode);
    }

    public async Task<string> CreateChartMessageAsync(string queryText)
    {
        var resolutionResult = await ResolveStockAsync(queryText, "!주식차트");
        if (resolutionResult.Message != null) return resolutionResult.Message;

        var resolvedStock = resolutionResult.Stock!;
        var chartNode = await FetchChartNodeAsync(resolvedStock);
        if (chartNode == null) return StockInformationUnavailableMessage;

        return FormatChartMessage(resolvedStock, chartNode);
    }

    public async Task<string> CreateNewsMessageAsync(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText)) return await CreateMainNewsMessageAsync();

        var resolutionResult = await ResolveStockAsync(queryText, "!주식뉴스");
        if (resolutionResult.Message != null) return resolutionResult.Message;

        var resolvedStock = resolutionResult.Stock!;
        var newsNode = await FetchStockNewsNodeAsync(resolvedStock);
        if (newsNode == null) return StockInformationUnavailableMessage;

        return FormatStockNewsMessage(resolvedStock, newsNode);
    }

    public async Task<string> CreateMarketMessageAsync(string queryText)
    {
        var marketContext = CreateMarketContext(queryText);
        if (marketContext == null) return CreateMarketUsageMessage();

        return marketContext.MarketType switch
        {
            StockMarketType.DomesticIndex => await CreateDomesticIndexMessageAsync(),
            StockMarketType.DomesticPopular => await CreatePopularStockMessageAsync("🇰🇷 국내 인기 종목", "KOR", true),
            StockMarketType.DomesticMarketValue => await CreateDomesticMarketValueMessageAsync(),
            StockMarketType.ForeignPopular => await CreatePopularStockMessageAsync("🌎 미국 인기 종목", "USA", false),
            StockMarketType.ForeignMarketValue => await CreateForeignMarketValueMessageAsync(marketContext.StockExchangeType),
            _ => CreateMarketUsageMessage()
        };
    }

    private async Task<StockResolutionResult> ResolveStockAsync(string queryText, string command)
    {
        if (string.IsNullOrWhiteSpace(queryText)) return new StockResolutionResult(null, CreateStockUsageMessage(command));

        var trimmedQueryText = queryText.Trim();
        var requestAddress = $"{FrontServiceBaseAddress}/search/autoComplete?query={Uri.EscapeDataString(trimmedQueryText)}&target=stock";
        var searchNode = await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/search");
        if (searchNode == null)
        {
            return TryCreateDirectStock(trimmedQueryText, out var fallbackDirectStock) ? new StockResolutionResult(fallbackDirectStock, null) : new StockResolutionResult(null, StockInformationUnavailableMessage);
        }

        var searchStocks = EnumerateItems(GetPropertyNode(searchNode, "items"))
            .Where(IsStockSearchItem)
            .Select(searchItem => MapSearchStock(trimmedQueryText, searchItem))
            .Where(searchStock => !string.IsNullOrWhiteSpace(searchStock.Name) || !string.IsNullOrWhiteSpace(searchStock.Code))
            .ToList();

        if (searchStocks.Count == 0 && TryCreateDirectStock(trimmedQueryText, out var directStock)) return new StockResolutionResult(directStock, null);

        if (searchStocks.Count == 0) return new StockResolutionResult(null, CreateNoStockFoundMessage(trimmedQueryText, command));

        var normalizedQueryText = NormalizeSearchText(trimmedQueryText);
        var exactSearchStocks = searchStocks
            .Where(searchStock => IsExactStockMatch(searchStock, normalizedQueryText))
            .ToList();

        if (exactSearchStocks.Count > 0) return new StockResolutionResult(exactSearchStocks[0], null);
        if (searchStocks.Count == 1) return new StockResolutionResult(searchStocks[0], null);

        return new StockResolutionResult(null, CreateAmbiguousStockMessage(trimmedQueryText, command, searchStocks));
    }

    private async Task<JsonNode?> FetchBasicStockNodeAsync(ResolvedStock resolvedStock)
    {
        if (resolvedStock.IsDomestic)
        {
            var requestAddress = $"{FrontServiceBaseAddress}/stock/domestic/basic?code={Uri.EscapeDataString(resolvedStock.Code)}&endType=stock";
            return await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/domestic/stock/{Uri.EscapeDataString(resolvedStock.Code)}/total");
        }

        var requestCode = GetRequestStockCode(resolvedStock);
        var foreignRequestAddress = $"{ForeignServiceBaseAddress}/stock/{Uri.EscapeDataString(requestCode)}/basic";
        return await FetchJsonNodeAsync(foreignRequestAddress, $"{MobileStockBaseAddress}/worldstock/stock/{Uri.EscapeDataString(requestCode)}/total");
    }

    private async Task<JsonNode?> FetchIntegrationStockNodeAsync(ResolvedStock resolvedStock)
    {
        if (resolvedStock.IsDomestic)
        {
            var requestAddress = $"{FrontServiceBaseAddress}/stock/domestic/integration?code={Uri.EscapeDataString(resolvedStock.Code)}&endType=stock";
            return await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/domestic/stock/{Uri.EscapeDataString(resolvedStock.Code)}/total");
        }

        var requestCode = GetRequestStockCode(resolvedStock);
        var foreignRequestAddress = $"{ForeignServiceBaseAddress}/stock/{Uri.EscapeDataString(requestCode)}/integration";
        return await FetchJsonNodeAsync(foreignRequestAddress, $"{MobileStockBaseAddress}/worldstock/stock/{Uri.EscapeDataString(requestCode)}/total");
    }

    private async Task<JsonNode?> FetchRealtimeStockNodeAsync(ResolvedStock resolvedStock)
    {
        JsonNode? realtimeNode;
        if (resolvedStock.IsDomestic)
        {
            var requestAddress = $"{FrontServiceBaseAddress}/realTime/marketPrice?itemCodes={Uri.EscapeDataString(resolvedStock.Code)}&endType=stock&stockType=domestic";
            realtimeNode = await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/domestic/stock/{Uri.EscapeDataString(resolvedStock.Code)}/total");
        }
        else
        {
            var requestCode = GetRequestStockCode(resolvedStock);
            var requestAddress = $"{PollingServiceBaseAddress}/api/realtime/worldstock/stock/{Uri.EscapeDataString(requestCode)}";
            realtimeNode = await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/worldstock/stock/{Uri.EscapeDataString(requestCode)}/total");
        }

        return EnumerateItems(GetPropertyNode(realtimeNode, "datas")).FirstOrDefault();
    }

    private async Task<JsonNode?> FetchDomesticTrendNodeAsync(ResolvedStock resolvedStock)
    {
        var requestAddress = $"{FrontServiceBaseAddress}/stock/domestic/trend?code={Uri.EscapeDataString(resolvedStock.Code)}&marketType=KRX&pageSize=5";
        return await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/domestic/stock/{Uri.EscapeDataString(resolvedStock.Code)}/total");
    }

    private async Task<JsonNode?> FetchChartNodeAsync(ResolvedStock resolvedStock)
    {
        if (resolvedStock.IsDomestic)
        {
            var requestAddress = $"{FrontServiceBaseAddress}/chart/domestic/stock/end?code={Uri.EscapeDataString(resolvedStock.Code)}&chartInfoType=item&scriptChartType=candleDay";
            return await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/domestic/stock/{Uri.EscapeDataString(resolvedStock.Code)}/total");
        }

        var requestCode = GetRequestStockCode(resolvedStock);
        var stockExchangeType = FirstNonEmpty(resolvedStock.MarketCode, "NASDAQ");
        var foreignRequestAddress = $"{ForeignServiceBaseAddress}/chart/foreign/item/{Uri.EscapeDataString(requestCode)}?periodType=dayCandle&stockExchangeType={Uri.EscapeDataString(stockExchangeType)}";
        return await FetchJsonNodeAsync(foreignRequestAddress, $"{MobileStockBaseAddress}/worldstock/stock/{Uri.EscapeDataString(requestCode)}/total");
    }

    private async Task<JsonNode?> FetchStockNewsNodeAsync(ResolvedStock resolvedStock)
    {
        if (resolvedStock.IsDomestic)
        {
            var requestAddress = $"{FrontServiceBaseAddress}/news/list/integration?itemCode={Uri.EscapeDataString(resolvedStock.Code)}";
            return await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/domestic/stock/{Uri.EscapeDataString(resolvedStock.Code)}/total");
        }

        var requestCode = GetRequestStockCode(resolvedStock);
        var foreignRequestAddress = $"{ForeignServiceBaseAddress}/news/integration/{Uri.EscapeDataString(requestCode)}";
        return await FetchJsonNodeAsync(foreignRequestAddress, $"{MobileStockBaseAddress}/worldstock/stock/{Uri.EscapeDataString(requestCode)}/total");
    }

    private async Task<JsonNode?> FetchJsonNodeAsync(string requestAddress, string refererAddress)
    {
        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Get, requestAddress);
            ConfigureRequestHeaders(requestMessage, refererAddress);

            using var responseMessage = await _stockClient.SendAsync(requestMessage);
            var responseContent = await responseMessage.Content.ReadAsStringAsync();
            if (!responseMessage.IsSuccessStatusCode)
            {
                logger.LogError("[STOCK] Request failed with status code {StatusCode}. Address: {RequestAddress}", responseMessage.StatusCode, requestAddress);
                return null;
            }

            var rootNode = JsonNode.Parse(responseContent);
            if (rootNode == null) return null;

            if (IsFrontServiceFailure(rootNode, out var failureMessage))
            {
                logger.LogError("[STOCK] Front service failed. Address: {RequestAddress}, Message: {FailureMessage}", requestAddress, failureMessage);
                return null;
            }

            return UnwrapResultNode(rootNode);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException or UriFormatException)
        {
            logger.LogError(exception, "[STOCK] Error fetching stock data. Address: {RequestAddress}", requestAddress);
            return null;
        }
    }

    private static void ConfigureRequestHeaders(HttpRequestMessage requestMessage, string refererAddress)
    {
        requestMessage.Headers.Accept.Clear();
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        requestMessage.Headers.UserAgent.Clear();
        requestMessage.Headers.UserAgent.ParseAdd(UserAgentHeaderValue);
        requestMessage.Headers.Referrer = new Uri(refererAddress);
    }

    private async Task<string> CreateMainNewsMessageAsync()
    {
        var requestAddress = $"{FrontServiceBaseAddress}/news/clusters";
        var newsClusterNode = await FetchJsonNodeAsync(requestAddress, MobileStockBaseAddress);
        if (newsClusterNode == null) return StockInformationUnavailableMessage;

        var newsNodes = EnumerateItems(newsClusterNode)
            .Select(newsCluster => EnumerateItems(GetPropertyNode(newsCluster, "articles")).FirstOrDefault() ?? newsCluster)
            .Take(DisplayLimit)
            .ToList();
        return FormatNewsItemsMessage("📰 증권 주요 뉴스", newsNodes);
    }

    private async Task<string> CreateDomesticIndexMessageAsync()
    {
        var requestAddress = $"{FrontServiceBaseAddress}/domestic/index/majors";
        var indexNode = await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/domestic");
        if (indexNode == null) return StockInformationUnavailableMessage;

        var indexNodes = EnumerateItems(indexNode).Take(DisplayLimit).ToList();
        if (indexNodes.Count == 0) return StockInformationUnavailableMessage;

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("📈 국내 주요 지수");
        stringBuilder.AppendLine();

        for (var index = 0; index < indexNodes.Count; index++) stringBuilder.AppendLine($"{index + 1}. {FormatIndexLine(indexNodes[index])}");

        return stringBuilder.ToString().TrimEnd();
    }

    private async Task<string> CreatePopularStockMessageAsync(string title, string nationType, bool isDomestic)
    {
        var requestAddress = $"{FrontServiceBaseAddress}/market/popularStock?nationType={Uri.EscapeDataString(nationType)}";
        var popularNode = await FetchJsonNodeAsync(requestAddress, MobileStockBaseAddress);
        if (popularNode == null) return StockInformationUnavailableMessage;

        var stockNodes = EnumerateItems(GetPropertyNode(popularNode, "datas")).Take(DisplayLimit).ToList();
        return FormatMarketStockListMessage(title, stockNodes, isDomestic, false);
    }

    private async Task<string> CreateDomesticMarketValueMessageAsync()
    {
        var requestAddress = $"{FrontServiceBaseAddress}/domestic/stock/list?sortType=marketValue&category=all&domesticStockExchangeType=KRX&page=1&pageSize=50";
        var stockListNode = await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/domestic/home/capitalization/total");
        if (stockListNode == null) return StockInformationUnavailableMessage;

        var stockNodes = EnumerateItems(GetPropertyNode(stockListNode, "stocks")).Take(DisplayLimit).ToList();
        return FormatMarketStockListMessage("🇰🇷 국내 시가총액 상위", stockNodes, true, true);
    }

    private async Task<string> CreateForeignMarketValueMessageAsync(string? stockExchangeType)
    {
        if (!string.IsNullOrWhiteSpace(stockExchangeType))
        {
            var stockListNode = await FetchForeignMarketValueNodeAsync(stockExchangeType);
            if (stockListNode == null) return StockInformationUnavailableMessage;

            var stockNodes = EnumerateItems(GetPropertyNode(stockListNode, "stocks")).Take(DisplayLimit).ToList();
            return FormatMarketStockListMessage($"🌎 {GetStockExchangeDisplayName(stockExchangeType)} 시가총액 상위", stockNodes, false, true);
        }

        var stockListTasks = new[]
        {
            FetchForeignMarketValueNodeAsync("NASDAQ"),
            FetchForeignMarketValueNodeAsync("NYSE"),
            FetchForeignMarketValueNodeAsync("AMEX")
        };
        var stockListNodes = await Task.WhenAll(stockListTasks);
        var mergedStockNodes = stockListNodes
            .Where(stockListNode => stockListNode != null)
            .SelectMany(stockListNode => EnumerateItems(GetPropertyNode(stockListNode, "stocks")))
            .OrderByDescending(GetMarketValueNumber)
            .Take(DisplayLimit)
            .ToList();

        return FormatMarketStockListMessage("🌎 미국 시가총액 상위", mergedStockNodes, false, true);
    }

    private async Task<JsonNode?> FetchForeignMarketValueNodeAsync(string stockExchangeType)
    {
        var requestAddress = $"{FrontServiceBaseAddress}/worldstock/exchange/stock/list?stockExchangeType={Uri.EscapeDataString(stockExchangeType)}&stockPriceSortType=marketValue&page=1&pageSize=50";
        return await FetchJsonNodeAsync(requestAddress, $"{MobileStockBaseAddress}/worldstock/home/USA/marketValue/{Uri.EscapeDataString(stockExchangeType)}");
    }

    private static string FormatSummaryMessage(ResolvedStock resolvedStock, JsonNode basicStockNode, JsonNode? realtimeStockNode)
    {
        var stockNode = realtimeStockNode ?? basicStockNode;
        var stockName = GetStockName(resolvedStock, basicStockNode, stockNode);
        var displayCode = GetDisplayStockCode(resolvedStock, basicStockNode, stockNode);
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"📈 {FormatNameAndCode(stockName, displayCode)}");
        stringBuilder.AppendLine();
        AppendValueLine(stringBuilder, "현재가", FormatPrice(stockNode, resolvedStock.IsDomestic));
        AppendValueLine(stringBuilder, "전일대비", FormatChangeText(stockNode));
        AppendValueLine(stringBuilder, "거래량", FormatNumberText(GetText(stockNode, "accumulatedTradingVolume", "executedVolume")));
        AppendValueLine(stringBuilder, "시가총액", FormatMarketValue(stockNode));
        AppendValueLine(stringBuilder, "장 상태", FormatMarketStatusText(GetText(stockNode, "marketStatus")));
        AppendValueLine(stringBuilder, "거래 시각", FormatDateTimeText(GetText(stockNode, "localTradedAt")));
        stringBuilder.AppendLine();
        stringBuilder.AppendLine($"더보기: !주식상세 {stockName}");
        stringBuilder.AppendLine($"뉴스: !주식뉴스 {stockName}");

        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatDetailMessage(ResolvedStock resolvedStock, JsonNode basicStockNode, JsonNode? integrationStockNode, JsonNode? trendNode)
    {
        var informationSourceNode = integrationStockNode ?? basicStockNode;
        var stockName = GetStockName(resolvedStock, basicStockNode, informationSourceNode);
        var displayCode = GetDisplayStockCode(resolvedStock, basicStockNode, informationSourceNode);
        var informationItems = EnumerateItems(GetFirstPropertyNode(integrationStockNode, "totalInfos", "stockItemTotalInfos"))
            .Concat(EnumerateItems(GetFirstPropertyNode(basicStockNode, "totalInfos", "stockItemTotalInfos")))
            .ToList();
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"📊 {FormatNameAndCode(stockName, displayCode)}");
        stringBuilder.AppendLine();
        AppendValueLine(stringBuilder, "현재가", FormatPrice(basicStockNode, resolvedStock.IsDomestic));
        AppendValueLine(stringBuilder, "전일대비", FormatChangeText(basicStockNode));
        AppendValueLine(stringBuilder, "거래소", GetExchangeText(basicStockNode));
        AppendValueLine(stringBuilder, "시가총액", FirstNonEmpty(FindInformationValue(informationItems, "marketValue", "시총"), FormatMarketValue(basicStockNode)));

        var metricLines = CreateMetricLines(informationItems);
        if (metricLines.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("주요 지표");
            foreach (var metricLine in metricLines) stringBuilder.AppendLine(metricLine);
        }

        var consensusLines = CreateConsensusLines(GetPropertyNode(integrationStockNode, "consensusInfo"), resolvedStock.IsDomestic);
        if (consensusLines.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("컨센서스");
            foreach (var consensusLine in consensusLines) stringBuilder.AppendLine(consensusLine);
        }

        var latestTrendNode = EnumerateItems(trendNode).FirstOrDefault() ?? EnumerateItems(GetPropertyNode(integrationStockNode, "dealTrendInfos")).FirstOrDefault();
        var trendLines = CreateTrendLines(latestTrendNode);
        if (trendLines.Count > 0)
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("투자자 동향");
            foreach (var trendLine in trendLines) stringBuilder.AppendLine(trendLine);
        }

        var overviewText = TruncateText(GetText(integrationStockNode, "corporateOverview", "description"), 240);
        overviewText = FirstNonEmpty(overviewText, TruncateText(GetText(GetPropertyNode(integrationStockNode, "summaries"), "summary"), 240));
        if (!string.IsNullOrWhiteSpace(overviewText))
        {
            stringBuilder.AppendLine();
            stringBuilder.AppendLine("기업 개요");
            stringBuilder.AppendLine(overviewText);
        }

        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatChartMessage(ResolvedStock resolvedStock, JsonNode chartNode)
    {
        var priceInformationNodes = EnumerateItems(GetPropertyNode(chartNode, "priceInfos")).ToList();
        if (priceInformationNodes.Count == 0) return StockInformationUnavailableMessage;

        var displayNodes = priceInformationNodes
            .Skip(Math.Max(0, priceInformationNodes.Count - DisplayLimit))
            .ToList();
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"📉 {FormatNameAndCode(resolvedStock.Name, GetDisplayStockCode(resolvedStock, null, null))} 일봉");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("최근 가격");

        for (var index = 0; index < displayNodes.Count; index++)
        {
            var priceInformationNode = displayNodes[index];
            var closePrice = FormatNumberText(GetText(priceInformationNode, "closePrice"));
            if (!resolvedStock.IsDomestic) closePrice = AppendSuffix(closePrice, FormatCurrencySuffix(GetCurrencyText(priceInformationNode, resolvedStock.IsDomestic)));

            stringBuilder.AppendLine($"{index + 1}. {FormatDateTimeText(GetText(priceInformationNode, "localDate"))} | " + $"종가 {closePrice} | " + $"거래량 {FormatNumberText(GetText(priceInformationNode, "accumulatedTradingVolume"))}");
        }

        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatStockNewsMessage(ResolvedStock resolvedStock, JsonNode newsNode)
    {
        List<JsonNode> newsNodes;
        if (resolvedStock.IsDomestic)
        {
            newsNodes = EnumerateItems(GetPropertyNode(newsNode, "stockNewsList")).ToList();
            if (newsNodes.Count == 0) newsNodes = EnumerateItems(GetPropertyNode(newsNode, "rankNewsList")).ToList();
        }
        else
        {
            newsNodes = EnumerateItems(GetPropertyNode(newsNode, "stockNews"))
                .SelectMany(stockNewsGroup => EnumerateItems(GetPropertyNode(stockNewsGroup, "items")))
                .ToList();
            if (newsNodes.Count == 0) newsNodes = EnumerateItems(GetPropertyNode(newsNode, "rankNews")).ToList();
        }

        return FormatNewsItemsMessage($"📰 {resolvedStock.Name} 뉴스", newsNodes.Take(DisplayLimit).ToList());
    }

    private static string FormatNewsItemsMessage(string title, IReadOnlyList<JsonNode> newsNodes)
    {
        if (newsNodes.Count == 0) return StockInformationUnavailableMessage;

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(title);
        stringBuilder.AppendLine();

        for (var index = 0; index < newsNodes.Count; index++)
        {
            var newsNode = newsNodes[index];
            stringBuilder.AppendLine($"{index + 1}. {CleanText(GetText(newsNode, "titleFull", "title", "tit", "rawTitle"))}");
            stringBuilder.AppendLine($"   {JoinNonEmpty(" | ", GetText(newsNode, "officeName", "ohnm"), FormatDateTimeText(GetText(newsNode, "datetime", "dt", "dtSvc", "modDateTime", "regDateTime")))}");
        }

        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatIndexLine(JsonNode indexNode)
    {
        var nameAndCode = FormatNameAndCode(GetText(indexNode, "stockName", "name"), GetText(indexNode, "itemCode", "symbolCode", "reutersCode"));
        return JoinNonEmpty(" | ", nameAndCode, FormatNumberText(GetText(indexNode, "closePrice", "currentPrice")), FormatChangeText(indexNode), FormatMarketStatusText(GetText(indexNode, "marketStatus")));
    }

    private static string FormatMarketStockListMessage(string title, IReadOnlyList<JsonNode> stockNodes, bool isDomestic, bool shouldIncludeMarketValue)
    {
        if (stockNodes.Count == 0) return StockInformationUnavailableMessage;

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(title);
        stringBuilder.AppendLine();

        for (var index = 0; index < stockNodes.Count; index++)
        {
            var stockNode = stockNodes[index];
            var stockLine = FormatMarketStockLine(stockNode, isDomestic, shouldIncludeMarketValue);
            stringBuilder.AppendLine($"{index + 1}. {stockLine}");
        }

        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatMarketStockLine(JsonNode stockNode, bool isDomestic, bool shouldIncludeMarketValue)
    {
        var nameAndCode = FormatNameAndCode(GetText(stockNode, "stockName", "name", "stockNameEng"), isDomestic ? GetText(stockNode, "itemCode", "symbolCode", "code", "reutersCode") : GetText(stockNode, "reutersCode", "id", "code", "symbolCode"));
        var values = new List<string>
        {
            nameAndCode,
            FormatPrice(stockNode, isDomestic),
            FormatChangeText(stockNode)
        };

        if (shouldIncludeMarketValue) values.Add(AppendLabel("시총", FormatMarketValue(stockNode)));
        return JoinNonEmpty(" | ", values.ToArray());
    }

    private static List<string> CreateMetricLines(IReadOnlyList<JsonNode> informationItems)
    {
        var metricLines = new List<string>();
        AppendListLine(metricLines, "PER", FindInformationValue(informationItems, "per", "PER"));
        AppendListLine(metricLines, "EPS", FindInformationValue(informationItems, "eps", "EPS"));
        AppendListLine(metricLines, "전일", FindInformationValue(informationItems, "lastClosePrice", "basePrice", "전일"));

        var highPrice = FindInformationValue(informationItems, "highPrice", "고가");
        var lowPrice = FindInformationValue(informationItems, "lowPrice", "저가");
        AppendListLine(metricLines, "고가/저가", JoinNonEmpty(" / ", highPrice, lowPrice));
        return metricLines;
    }

    private static List<string> CreateConsensusLines(JsonNode? consensusNode, bool isDomestic)
    {
        var consensusLines = new List<string>();
        if (consensusNode == null) return consensusLines;

        AppendListLine(consensusLines, "투자의견", GetText(consensusNode, "recommMean"));

        var targetPrice = FormatNumberText(GetText(consensusNode, "priceTargetMean"));
        var targetPriceSuffix = isDomestic ? "원" : FormatCurrencySuffix(GetCurrencyText(consensusNode, isDomestic));
        AppendListLine(consensusLines, "목표가 평균", AppendSuffix(targetPrice, targetPriceSuffix));
        return consensusLines;
    }

    private static List<string> CreateTrendLines(JsonNode? trendNode)
    {
        var trendLines = new List<string>();
        if (trendNode == null) return trendLines;

        AppendListLine(trendLines, "기준일", FormatDateTimeText(GetText(trendNode, "bizdate")));
        AppendListLine(trendLines, "외국인", GetText(trendNode, "foreignerPureBuyQuant"));
        AppendListLine(trendLines, "기관", GetText(trendNode, "organPureBuyQuant"));
        AppendListLine(trendLines, "개인", GetText(trendNode, "individualPureBuyQuant"));
        return trendLines;
    }

    private static StockMarketContext? CreateMarketContext(string queryText)
    {
        if (string.IsNullOrWhiteSpace(queryText)) return null;

        var normalizedQueryText = NormalizeSearchText(queryText);
        var wantsIndex = ContainsAny(normalizedQueryText, "지수", "INDEX");
        var wantsPopular = ContainsAny(normalizedQueryText, "인기", "POPULAR");
        var wantsMarketValue = ContainsAny(normalizedQueryText, "시총", "시가총액", "MARKETVALUE", "상위");
        var wantsDomestic = ContainsAny(normalizedQueryText, "국내", "한국", "KOR", "KRX");
        var wantsForeign = ContainsAny(normalizedQueryText, "미국", "해외", "USA", "US");
        var stockExchangeType = FindStockExchangeType(normalizedQueryText);

        if (wantsIndex && (wantsForeign || stockExchangeType != null)) return null;
        if (wantsIndex) return new StockMarketContext(StockMarketType.DomesticIndex, null);
        if (wantsDomestic && wantsPopular) return new StockMarketContext(StockMarketType.DomesticPopular, null);
        if (wantsDomestic && wantsMarketValue) return new StockMarketContext(StockMarketType.DomesticMarketValue, null);
        if (wantsDomestic) return new StockMarketContext(StockMarketType.DomesticIndex, null);
        if (stockExchangeType != null) return new StockMarketContext(StockMarketType.ForeignMarketValue, stockExchangeType);
        if (wantsForeign && wantsMarketValue) return new StockMarketContext(StockMarketType.ForeignMarketValue, null);
        if (wantsForeign || wantsPopular) return new StockMarketContext(StockMarketType.ForeignPopular, null);

        return null;
    }

    private static string? FindStockExchangeType(string normalizedQueryText)
    {
        if (ContainsAny(normalizedQueryText, "나스닥", "NASDAQ")) return "NASDAQ";
        if (ContainsAny(normalizedQueryText, "뉴욕", "NYSE")) return "NYSE";
        if (ContainsAny(normalizedQueryText, "아멕스", "AMEX")) return "AMEX";
        return null;
    }

    private static bool IsStockSearchItem(JsonNode searchItem) =>
        string.Equals(GetText(searchItem, "category"), "stock", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(GetText(searchItem, "reutersCode", "code"));

    private static ResolvedStock MapSearchStock(string searchText, JsonNode searchItem)
    {
        var code = GetText(searchItem, "code");
        var reutersCode = FirstNonEmpty(GetText(searchItem, "reutersCode"), code);
        var nationCode = GetText(searchItem, "nationCode");
        var isDomestic = string.Equals(nationCode, "KOR", StringComparison.OrdinalIgnoreCase) || IsDomesticStockCode(code);

        return new ResolvedStock(searchText, GetText(searchItem, "name"), code, reutersCode, FirstNonEmpty(GetText(searchItem, "symbolCode"), code), nationCode, GetText(searchItem, "typeCode"), GetText(searchItem, "typeName"), isDomestic);
    }

    private static bool TryCreateDirectStock(string queryText, out ResolvedStock resolvedStock)
    {
        var trimmedQueryText = queryText.Trim();
        if (IsDomesticStockCode(trimmedQueryText))
        {
            resolvedStock = new ResolvedStock(trimmedQueryText, trimmedQueryText, trimmedQueryText, trimmedQueryText, trimmedQueryText, "KOR", "KRX", "KRX", true);
            return true;
        }

        if (trimmedQueryText.Contains('.', StringComparison.Ordinal))
        {
            var symbolCode = trimmedQueryText.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? trimmedQueryText;
            resolvedStock = new ResolvedStock(trimmedQueryText, trimmedQueryText, symbolCode, trimmedQueryText, symbolCode, "USA", "NASDAQ", "NASDAQ", false);
            return true;
        }

        resolvedStock = null!;
        return false;
    }

    private static bool IsExactStockMatch(ResolvedStock resolvedStock, string normalizedQueryText) =>
        NormalizeSearchText(resolvedStock.Name).Equals(normalizedQueryText, StringComparison.OrdinalIgnoreCase) || NormalizeSearchText(resolvedStock.Code).Equals(normalizedQueryText, StringComparison.OrdinalIgnoreCase) || NormalizeSearchText(resolvedStock.ReutersCode).Equals(normalizedQueryText, StringComparison.OrdinalIgnoreCase) || NormalizeSearchText(resolvedStock.SymbolCode).Equals(normalizedQueryText, StringComparison.OrdinalIgnoreCase);

    private static string CreateStockUsageMessage(string command) =>
        $"사용법: {command} [종목명/코드/티커]\n예시: {command} 삼성전자, {command} AAPL, {command} 005930";

    private static string CreateMarketUsageMessage() =>
        "사용법: !증시 [국내/미국/나스닥/뉴욕/아멕스] [인기/시총/지수]\n예시: !증시 국내 지수, !증시 미국 인기, !증시 나스닥 시총";

    private static string CreateNoStockFoundMessage(string queryText, string command) =>
        $"'{queryText}'에 해당하는 종목을 찾지 못했습니다.\n예시: {command} 삼성전자, {command} AAPL, {command} 005930";

    private static string CreateAmbiguousStockMessage(string queryText, string command, IReadOnlyList<ResolvedStock> searchStocks)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine($"'{queryText}' 검색 결과가 여러 개입니다.");
        stringBuilder.AppendLine();

        var displayStocks = searchStocks.Take(DisplayLimit).ToList();
        for (var index = 0; index < displayStocks.Count; index++)
        {
            var searchStock = displayStocks[index];
            stringBuilder.AppendLine($"{index + 1}. {FormatNameAndCode(searchStock.Name, GetDisplayStockCode(searchStock, null, null))} | {JoinNonEmpty(" / ", searchStock.NationCode, FirstNonEmpty(searchStock.MarketCode, searchStock.MarketName))}");
        }

        stringBuilder.AppendLine();
        stringBuilder.AppendLine("더 구체적으로 입력해주세요.");
        stringBuilder.AppendLine($"예시: {command} 삼성전자, {command} 005930");
        return stringBuilder.ToString().TrimEnd();
    }

    private static JsonNode? UnwrapResultNode(JsonNode rootNode)
    {
        var resultNode = GetPropertyNode(rootNode, "result");
        return resultNode ?? rootNode;
    }

    private static bool IsFrontServiceFailure(JsonNode rootNode, out string failureMessage)
    {
        failureMessage = string.Empty;
        var isSuccess = GetText(rootNode, "isSuccess");
        if (!string.Equals(isSuccess, "false", StringComparison.OrdinalIgnoreCase)) return false;

        failureMessage = JoinNonEmpty(" / ", GetText(rootNode, "detailCode"), GetText(rootNode, "message"));
        return true;
    }

    private static IEnumerable<JsonNode> EnumerateItems(JsonNode? jsonNode)
    {
        if (jsonNode is JsonArray jsonArray)
        {
            foreach (var itemNode in jsonArray)
            {
                if (itemNode != null) yield return itemNode;
            }

            yield break;
        }

        if (jsonNode is JsonObject) yield return jsonNode;
    }

    private static JsonNode? GetFirstPropertyNode(JsonNode? jsonNode, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var propertyNode = GetPropertyNode(jsonNode, propertyName);
            if (propertyNode != null) return propertyNode;
        }

        return null;
    }

    private static JsonNode? GetPropertyNode(JsonNode? jsonNode, string propertyName)
    {
        if (jsonNode is not JsonObject jsonObject) return null;
        if (jsonObject.TryGetPropertyValue(propertyName, out var propertyNode)) return propertyNode;

        foreach (var property in jsonObject)
        {
            if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase)) return property.Value;
        }

        return null;
    }

    private static string GetText(JsonNode? jsonNode, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var propertyText = GetNodeText(GetPropertyNode(jsonNode, propertyName));
            if (!string.IsNullOrWhiteSpace(propertyText)) return CleanText(propertyText);
        }

        return string.Empty;
    }

    private static string GetNodeText(JsonNode? jsonNode)
    {
        if (jsonNode == null) return string.Empty;
        if (jsonNode is not JsonValue) return string.Empty;

        var valueText = jsonNode.ToString();
        return string.Equals(valueText, "null", StringComparison.OrdinalIgnoreCase) ? string.Empty : valueText.Trim();
    }

    private static string GetStockName(ResolvedStock resolvedStock, JsonNode? firstStockNode, JsonNode? secondStockNode) =>
        FirstNonEmpty(GetText(secondStockNode, "stockName", "name"), GetText(firstStockNode, "stockName", "name"), resolvedStock.Name);

    private static string GetDisplayStockCode(ResolvedStock resolvedStock, JsonNode? firstStockNode, JsonNode? secondStockNode)
    {
        if (resolvedStock.IsDomestic) return FirstNonEmpty(GetText(secondStockNode, "itemCode", "symbolCode", "code"), GetText(firstStockNode, "itemCode", "symbolCode", "code"), resolvedStock.Code);

        return FirstNonEmpty(GetText(secondStockNode, "reutersCode", "id"), GetText(firstStockNode, "reutersCode", "id"), resolvedStock.ReutersCode);
    }

    private static string GetRequestStockCode(ResolvedStock resolvedStock) =>
        resolvedStock.IsDomestic ? resolvedStock.Code : FirstNonEmpty(resolvedStock.ReutersCode, resolvedStock.Code);

    private static string FormatPrice(JsonNode? stockNode, bool isDomestic)
    {
        var price = FormatNumberText(GetText(stockNode, "closePrice", "currentPrice", "overPrice"));
        return AppendSuffix(price, FormatCurrencySuffix(GetCurrencyText(stockNode, isDomestic)));
    }

    private static string FormatChangeText(JsonNode? stockNode)
    {
        var direction = FirstNonEmpty(GetText(GetPropertyNode(stockNode, "compareToPreviousPrice"), "text"), ConvertFluctuationType(GetText(stockNode, "fluctuationsType", "fluctuationType")));
        var amount = FormatNumberText(GetText(stockNode, "compareToPreviousClosePrice", "fluctuations", "fluctuation"));
        var ratio = FormatRatioText(GetText(stockNode, "fluctuationsRatio", "fluctuationsRatioRaw"));
        var changeValue = JoinNonEmpty(" ", amount, FormatParenthesized(ratio));

        if (string.IsNullOrWhiteSpace(direction)) return AppendLabel("전일대비", changeValue);
        return JoinNonEmpty(" ", direction, changeValue);
    }

    private static string FormatMarketValue(JsonNode? stockNode)
    {
        var marketValue = FirstNonEmpty(GetText(stockNode, "marketValueHangeul"), FormatNumberText(GetText(stockNode, "marketValue", "marketValueFull")));
        var marketValueKoreanWon = GetText(stockNode, "marketValueKrwHangeul");
        return JoinNonEmpty(" / ", marketValue, marketValueKoreanWon);
    }

    private static string GetExchangeText(JsonNode? stockNode)
    {
        var exchangeNode = GetPropertyNode(stockNode, "stockExchangeType");
        return FirstNonEmpty(GetText(stockNode, "stockExchangeName", "stockExchangeType", "typeName", "typeCode"), GetText(exchangeNode, "name", "nameKor", "nameEng"));
    }

    private static string GetCurrencyText(JsonNode? stockNode, bool isDomestic)
    {
        if (isDomestic) return "원";

        var currencyNode = GetPropertyNode(stockNode, "currencyType");
        return FirstNonEmpty(GetText(stockNode, "currencyType"), GetText(currencyNode, "code", "name"));
    }

    private static string FindInformationValue(IReadOnlyList<JsonNode> informationItems, params string[] keysOrCodes)
    {
        foreach (var keyOrCode in keysOrCodes)
        {
            var informationItem = informationItems.FirstOrDefault(item => string.Equals(GetText(item, "code"), keyOrCode, StringComparison.OrdinalIgnoreCase) || string.Equals(GetText(item, "key", "name"), keyOrCode, StringComparison.OrdinalIgnoreCase));
            var informationValue = GetText(informationItem, "value", "text");
            if (!string.IsNullOrWhiteSpace(informationValue)) return informationValue;
        }

        return string.Empty;
    }

    private static decimal GetMarketValueNumber(JsonNode stockNode)
    {
        var marketValueText = GetText(stockNode, "marketValue", "marketValueFull").Replace(",", string.Empty);
        return decimal.TryParse(marketValueText, NumberStyles.Number, CultureInfo.InvariantCulture, out var marketValue) ? marketValue : 0;
    }

    private static string FormatNameAndCode(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name)) return code;
        if (string.IsNullOrWhiteSpace(code)) return name;
        return $"{name} ({code})";
    }

    private static string FormatNumberText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var trimmedValue = value.Trim();
        if (!ContainsOnlyNumberCharacters(trimmedValue)) return trimmedValue;

        var normalizedValue = trimmedValue.Replace(",", string.Empty);
        if (!decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return trimmedValue;
        if (!normalizedValue.Contains('.')) return number.ToString("N0", CultureInfo.InvariantCulture);

        return number.ToString("N4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
    }

    private static bool ContainsOnlyNumberCharacters(string value) =>
        value.All(character => char.IsDigit(character) || character is '-' or '+' or '.' or ',');

    private static string FormatRatioText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var formattedRatio = FormatNumberText(value);
        if (formattedRatio.Contains('%')) return formattedRatio;
        return $"{formattedRatio}%";
    }

    private static string FormatDateTimeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeOffset)) return dateTimeOffset.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        string[] compactFormats = ["yyyyMMddHHmmss", "yyyyMMddHHmm", "yyyyMMdd"];
        if (!DateTime.TryParseExact(value, compactFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime)) return value;
        return value.Length == 8 ? dateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : dateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string FormatMarketStatusText(string marketStatus)
    {
        if (string.IsNullOrWhiteSpace(marketStatus)) return string.Empty;

        return marketStatus.ToUpperInvariant() switch
        {
            "OPEN" => "장중",
            "CLOSE" => "마감",
            "PRE_OPEN" => "장전",
            "PRE_MARKET" => "프리마켓",
            "AFTER_MARKET" => "애프터마켓",
            _ => marketStatus
        };
    }

    private static string ConvertFluctuationType(string fluctuationType)
    {
        if (string.IsNullOrWhiteSpace(fluctuationType)) return string.Empty;

        return fluctuationType.ToUpperInvariant() switch
        {
            "RISING" or "RISE" => "상승",
            "FALLING" or "FALL" => "하락",
            "UNCHANGED" or "STEADY" => "보합",
            _ => fluctuationType
        };
    }

    private static string FormatCurrencySuffix(string currencyText) =>
        string.IsNullOrWhiteSpace(currencyText) ? string.Empty : currencyText == "원" ? "원" : $" {currencyText}";

    private static string FormatParenthesized(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"({value})";

    private static string AppendLabel(string label, string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"{label} {value}";

    private static string AppendSuffix(string value, string suffix)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (string.IsNullOrWhiteSpace(suffix)) return value;
        if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) return value;
        return $"{value}{suffix}";
    }

    private static void AppendValueLine(StringBuilder stringBuilder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        stringBuilder.AppendLine($"{label}: {value}");
    }

    private static void AppendListLine(List<string> lines, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        lines.Add($"- {label}: {value}");
    }

    private static string TruncateText(string value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var normalizedValue = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return normalizedValue.Length <= maximumLength ? normalizedValue : $"{normalizedValue[..maximumLength]}...";
    }

    private static string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return WebUtility.HtmlDecode(value).Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private static string NormalizeSearchText(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();

    private static bool ContainsAny(string value, params string[] candidates) =>
        candidates.Any(candidate => value.Contains(NormalizeSearchText(candidate), StringComparison.OrdinalIgnoreCase));

    private static bool IsDomesticStockCode(string value) =>
        value.Length == 6 && value.All(char.IsDigit);

    private static string GetStockExchangeDisplayName(string stockExchangeType) =>
        stockExchangeType.ToUpperInvariant() switch
        {
            "NASDAQ" => "나스닥",
            "NYSE" => "뉴욕거래소",
            "AMEX" => "아멕스",
            _ => stockExchangeType
        };

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string JoinNonEmpty(string separator, params string[] values) =>
        string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));

    private sealed record StockResolutionResult(ResolvedStock? Stock, string? Message);

    private sealed record ResolvedStock(string SearchText, string Name, string Code, string ReutersCode, string SymbolCode, string NationCode, string MarketCode, string MarketName, bool IsDomestic);

    private sealed record StockMarketContext(StockMarketType MarketType, string? StockExchangeType);

    private enum StockMarketType
    {
        DomesticIndex,
        DomesticPopular,
        DomesticMarketValue,
        ForeignPopular,
        ForeignMarketValue
    }

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_stock_summary", "Get a short stock summary by stock name, code, or ticker.", CreateStockQuerySchema("Stock name, code, or ticker to query.", true)),
        new("get_stock_detail", "Get detailed stock information by stock name, code, or ticker.", CreateStockQuerySchema("Stock name, code, or ticker to query.", true)),
        new("get_stock_chart", "Get recent stock chart summary by stock name, code, or ticker.", CreateStockQuerySchema("Stock name, code, or ticker to query.", true)),
        new("get_stock_news", "Get stock news. Query can be omitted for main market news.", CreateStockQuerySchema("Stock name, code, ticker, or empty for main market news.", false)),
        new("get_market_summary", "Get market summary for domestic or US market.", CreateStockQuerySchema("Market query such as domestic index, domestic popular, US popular, NASDAQ market cap.", true))
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        var queryText = DengAiToolJson.ReadString(arguments, "query") ?? string.Empty;

        return toolName switch
        {
            "get_stock_summary" => await CreateSummaryMessageAsync(queryText),
            "get_stock_detail" => await CreateDetailMessageAsync(queryText),
            "get_stock_chart" => await CreateChartMessageAsync(queryText),
            "get_stock_news" => await CreateNewsMessageAsync(queryText),
            "get_market_summary" => await CreateMarketMessageAsync(queryText),
            _ => "Unknown stock tool."
        };
    }

    private static DengAiJsonSchema CreateStockQuerySchema(string description, bool required) =>
        DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["query"] = DengAiJsonSchemaProperty.String(description)
        }, required ? ["query"] : null);

    #endregion
}
