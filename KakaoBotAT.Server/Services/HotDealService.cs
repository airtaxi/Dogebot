using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace KakaoBotAT.Server.Services;

public class HotDealService : IHotDealService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HotDealService> _logger;
    private static DateTime _lastFetchTime = DateTime.MinValue;
    private static List<HotDealItem>? _cachedDeals;
    private static readonly object _cacheLock = new();

    private const string HotDealUrl = "https://arca.live/b/hotdeal";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(3);

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
            List<HotDealItem> deals;

            lock (_cacheLock)
            {
                // Check if cache is still valid
                if (_cachedDeals != null && DateTime.UtcNow - _lastFetchTime < CacheDuration)
                {
                    _logger.LogInformation("[HOTDEAL] Using cached deals (age: {Age}s)", (DateTime.UtcNow - _lastFetchTime).TotalSeconds);
                    deals = _cachedDeals;
                }
                else
                {
                    deals = null!;
                }
            }

            // Fetch new deals if cache is invalid
            if (deals == null)
            {
                var response = await _httpClient.GetAsync(HotDealUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("[HOTDEAL] Failed to fetch hot deals. Status code: {StatusCode}", response.StatusCode);
                    return null;
                }

                var html = await response.Content.ReadAsStringAsync();
                deals = ParseHotDeals(html);

                if (deals.Count == 0)
                {
                    _logger.LogWarning("[HOTDEAL] No hot deals found on the page");
                    return null;
                }

                // Update cache
                lock (_cacheLock)
                {
                    _cachedDeals = deals;
                    _lastFetchTime = DateTime.UtcNow;
                }
            }

            var randomDeal = deals[Random.Shared.Next(deals.Count)];
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
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Select all deal rows (excluding notices)
            var dealRows = doc.DocumentNode.SelectNodes("//div[contains(@class, 'vrow') and contains(@class, 'hybrid') and not(contains(@class, 'notice'))]");

            if (dealRows == null)
            {
                _logger.LogWarning("[HOTDEAL] No deal rows found in HTML");
                return deals;
            }

            foreach (var row in dealRows)
            {
                try
                {
                    // Skip closed deals (deal-close class)
                    if (row.InnerHtml.Contains("deal-close"))
                        continue;

                    var deal = new HotDealItem();

                    // Extract link and title from <a class="title hybrid-title">
                    var titleLink = row.SelectSingleNode(".//a[contains(@class, 'hybrid-title')]");
                    if (titleLink != null)
                    {
                        var href = titleLink.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href))
                        {
                            deal.Link = $"https://arca.live{href.Split('?')[0]}";
                        }

                        // Get title text (exclude child elements like comment count)
                        var titleText = titleLink.InnerText;
                        // Clean up the title
                        deal.Title = System.Net.WebUtility.HtmlDecode(titleText).Trim();
                        // Remove comment count like [5]
                        deal.Title = System.Text.RegularExpressions.Regex.Replace(deal.Title, @"\[\d+\]", "").Trim();
                    }

                    // Extract price from <span class="deal-price">
                    var priceNode = row.SelectSingleNode(".//span[contains(@class, 'deal-price')]");
                    if (priceNode != null)
                    {
                        deal.Price = System.Net.WebUtility.HtmlDecode(priceNode.InnerText).Trim();
                    }

                    // Extract shipping from <span class="deal-delivery">
                    var deliveryNode = row.SelectSingleNode(".//span[contains(@class, 'deal-delivery')]");
                    if (deliveryNode != null)
                    {
                        deal.ShippingCost = System.Net.WebUtility.HtmlDecode(deliveryNode.InnerText).Trim();
                    }

                    // Extract store from <span class="deal-store">
                    var storeNode = row.SelectSingleNode(".//span[contains(@class, 'deal-store')]");
                    if (storeNode != null)
                    {
                        deal.Mall = System.Net.WebUtility.HtmlDecode(storeNode.InnerText).Trim();
                    }

                    // Only add if we have at least a title
                    if (!string.IsNullOrEmpty(deal.Title))
                    {
                        deals.Add(deal);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[HOTDEAL] Error parsing individual deal row");
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
}