using System.Text.RegularExpressions;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace Dogebot.Server.Services;

public partial class HotDealService(ILogger<HotDealService> logger) : IHotDealService, IDisposable
{
    private static DateTime _lastFetchTime = DateTime.MinValue;
    private static List<HotDealItem>? _cachedDeals;
    private static ChromeDriver? _driver;
    private static readonly Lock _cacheLock = new();
    private static readonly Lock _driverLock = new();

    private const string HotDealUrl = "https://arca.live/b/hotdeal";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(3);

    private static ChromeDriver GetOrCreateDriver()
    {
        lock (_driverLock)
        {
            if (_driver == null)
            {
                var options = new ChromeOptions();
                options.AddArgument("--headless");
                options.AddArgument("--no-sandbox");
                options.AddArgument("--disable-dev-shm-usage");
                options.AddArgument("--disable-gpu");
                options.AddArgument("--window-size=1920,1080");
                options.AddArgument("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                options.AddArgument("--disable-blink-features=AutomationControlled");
                options.AddExcludedArgument("enable-automation");
                options.AddAdditionalOption("useAutomationExtension", false);

                _driver = new ChromeDriver(options);
                _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
                _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            }
            return _driver;
        }
    }

    public async Task<IReadOnlyList<HotDealItem>> GetRecentHotDealsAsync()
    {
        try
        {
            List<HotDealItem> deals;

            lock (_cacheLock)
            {
                // Check if cache is still valid
                if (_cachedDeals != null && DateTime.UtcNow - _lastFetchTime < CacheDuration)
                {
                    logger.LogInformation("[HOTDEAL] Using cached deals (age: {Age}s)", (DateTime.UtcNow - _lastFetchTime).TotalSeconds);
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
                var html = await Task.Run(FetchPageWithSelenium);

                if (string.IsNullOrEmpty(html))
                {
                    logger.LogError("[HOTDEAL] Failed to fetch hot deals page");
                    return [];
                }

                deals = ParseHotDeals(html);

                if (deals.Count == 0)
                {
                    logger.LogWarning("[HOTDEAL] No hot deals found on the page");
                    return [];
                }

                // Update cache
                lock (_cacheLock)
                {
                    _cachedDeals = deals;
                    _lastFetchTime = DateTime.UtcNow;
                }
            }

            return deals;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HOTDEAL] Error fetching hot deals");
            return [];
        }
    }

    public async Task<HotDealItem?> GetRandomHotDealAsync()
    {
        try
        {
            var deals = await GetRecentHotDealsAsync();
            if (deals.Count == 0) return null;

            return deals[Random.Shared.Next(deals.Count)];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HOTDEAL] Error selecting random hot deal");
            return null;
        }
    }

    private string? FetchPageWithSelenium()
    {
        try
        {
            var driver = GetOrCreateDriver();
            
            lock (_driverLock)
            {
                driver.Navigate().GoToUrl(HotDealUrl);
                
                // Wait for the page to load
                Thread.Sleep(3000);
                
                var html = driver.PageSource;
                logger.LogInformation("[HOTDEAL] Successfully fetched page with Selenium");
                return html;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HOTDEAL] Error fetching page with Selenium");
            
            // Try to recreate driver on error
            lock (_driverLock)
            {
                try
                {
                    _driver?.Quit();
                }
                catch { }
                _driver = null;
            }
            
            return null;
        }
    }

    public DateTime? GetLastCacheTime()
    {
        lock (_cacheLock)
        {
            return _lastFetchTime == DateTime.MinValue ? null : _lastFetchTime;
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
                logger.LogWarning("[HOTDEAL] No deal rows found in HTML");
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
                        deal.Title = CommentCountRegex().Replace(deal.Title, "").Trim();
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
                    logger.LogWarning(ex, "[HOTDEAL] Error parsing individual deal row");
                }
            }

            logger.LogInformation("[HOTDEAL] Parsed {Count} hot deals from page", deals.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HOTDEAL] Error parsing hot deals HTML");
        }

        return deals;
    }

    public void Dispose()
    {
        lock (_driverLock)
        {
            try
            {
                _driver?.Quit();
                _driver?.Dispose();
            }
            catch { }
            _driver = null;
        }

        GC.SuppressFinalize(this);
    }

    [GeneratedRegex(@"\[\d+\]")]
    private static partial Regex CommentCountRegex();

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_random_hot_deal", "Get one random hot deal item from the cached or fetched hot deal list. Include the returned Link as plain URL text when introducing the item.", DengAiJsonSchema.Object()),
        new("get_recent_hot_deals", "Get the current first page of recent hot deal items for Deng AI RAG context. Use this when the user asks for recent hot deals, compares deals, or wants hot deal recommendations from the latest list. Include item links as plain URL text when recommending specific items.", DengAiJsonSchema.Object())
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "get_random_hot_deal" => await CreateRandomHotDealToolResultAsync(),
            "get_recent_hot_deals" => await CreateRecentHotDealsToolResultAsync(),
            _ => "Unknown hot deal tool."
        };
    }

    private async Task<string> CreateRandomHotDealToolResultAsync()
    {
        var hotDealItem = await GetRandomHotDealAsync();
        if (hotDealItem == null) return "핫딜 정보를 가져오지 못했습니다.";

        return DengAiToolJson.Serialize(new
        {
            hotDealItem.Title,
            hotDealItem.Price,
            hotDealItem.ShippingCost,
            hotDealItem.Mall,
            hotDealItem.Link,
            LastCacheTime = GetLastCacheTime()
        });
    }

    private async Task<string> CreateRecentHotDealsToolResultAsync()
    {
        var hotDealItems = await GetRecentHotDealsAsync();
        if (hotDealItems.Count == 0) return "핫딜 정보를 가져오지 못했습니다.";

        return DengAiToolJson.Serialize(new
        {
            HotDeals = hotDealItems.Select(hotDealItem => new
            {
                hotDealItem.Title,
                hotDealItem.Price,
                hotDealItem.ShippingCost,
                hotDealItem.Mall,
                hotDealItem.Link
            }).ToList(),
            LastCacheTime = GetLastCacheTime()
        });
    }

    #endregion
}
