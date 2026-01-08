using System.Text.RegularExpressions;

namespace KakaoBotAT.Server.Services;

public partial class HotDealService : IHotDealService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HotDealService> _logger;
    private readonly Random _random = new();

    private const string HotDealUrl = "https://arca.live/b/hotdeal";

    public HotDealService(IHttpClientFactory httpClientFactory, ILogger<HotDealService> logger)
    {
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ko-KR,ko;q=0.9,en-US;q=0.8,en;q=0.7");
        _logger = logger;
    }

    public async Task<HotDealItem?> GetRandomHotDealAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(HotDealUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[HOTDEAL] Failed to fetch hot deals. Status code: {StatusCode}", response.StatusCode);
                return null;
            }

            var html = await response.Content.ReadAsStringAsync();
            var deals = ParseHotDeals(html);

            if (deals.Count == 0)
            {
                _logger.LogWarning("[HOTDEAL] No hot deals found on the page");
                return null;
            }

            var randomDeal = deals[_random.Next(deals.Count)];
            return randomDeal;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HOTDEAL] Error fetching hot deals");
            return null;
        }
    }

    private List<HotDealItem> ParseHotDeals(string html)
    {
        var deals = new List<HotDealItem>();

        try
        {
            // Match article list items with hotdeal info
            // Pattern: <a class="vrow hybrid" href="/b/hotdeal/..." ...>
            var articlePattern = ArticleRegex();
            var articleMatches = articlePattern.Matches(html);

            foreach (Match match in articleMatches)
            {
                var articleHtml = match.Value;
                var href = match.Groups[1].Value;

                // Skip notice/pinned posts
                if (articleHtml.Contains("vrow-top") || articleHtml.Contains("notice"))
                    continue;

                var deal = new HotDealItem
                {
                    Link = $"https://arca.live{href}"
                };

                // Extract title from <span class="title">...</span>
                var titleMatch = TitleRegex().Match(articleHtml);
                if (titleMatch.Success)
                {
                    deal.Title = CleanHtml(titleMatch.Groups[1].Value).Trim();
                }

                // Extract price from deal-price span
                var priceMatch = PriceRegex().Match(articleHtml);
                if (priceMatch.Success)
                {
                    deal.Price = CleanHtml(priceMatch.Groups[1].Value).Trim();
                }

                // Extract shipping from deal-ship span
                var shippingMatch = ShippingRegex().Match(articleHtml);
                if (shippingMatch.Success)
                {
                    deal.ShippingCost = CleanHtml(shippingMatch.Groups[1].Value).Trim();
                }

                // Extract mall from deal-store span
                var mallMatch = MallRegex().Match(articleHtml);
                if (mallMatch.Success)
                {
                    deal.Mall = CleanHtml(mallMatch.Groups[1].Value).Trim();
                }

                // Only add if we have at least a title
                if (!string.IsNullOrEmpty(deal.Title))
                {
                    deals.Add(deal);
                }
            }

            _logger.LogInformation("[HOTDEAL] Parsed {Count} hot deals from page", deals.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HOTDEAL] Error parsing hot deals HTML");
        }

        return deals;
    }

    private static string CleanHtml(string html)
    {
        // Remove HTML tags
        var result = Regex.Replace(html, "<[^>]+>", "");
        // Decode HTML entities
        result = System.Net.WebUtility.HtmlDecode(result);
        return result.Trim();
    }

    [GeneratedRegex(@"<a[^>]*class=""[^""]*vrow[^""]*hybrid[^""]*""[^>]*href=""(/b/hotdeal/\d+)""[^>]*>.*?</a>", RegexOptions.Singleline)]
    private static partial Regex ArticleRegex();

    [GeneratedRegex(@"<span[^>]*class=""[^""]*title[^""]*""[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    [GeneratedRegex(@"<span[^>]*class=""[^""]*deal-price[^""]*""[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex PriceRegex();

    [GeneratedRegex(@"<span[^>]*class=""[^""]*deal-ship[^""]*""[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex ShippingRegex();

    [GeneratedRegex(@"<span[^>]*class=""[^""]*deal-store[^""]*""[^>]*>(.*?)</span>", RegexOptions.Singleline)]
    private static partial Regex MallRegex();
}
