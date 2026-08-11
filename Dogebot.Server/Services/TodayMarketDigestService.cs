using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public class TodayMarketDigestService(ILogger<TodayMarketDigestService> logger) : ITodayMarketDigestService
{
    private const string TodayMarketDigestUrl = "https://aikstockdata.com/data/public/today.json";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private static DateTime s_lastFetchTime = DateTime.MinValue;
    private static string? s_cachedDigest;
    private static readonly Lock s_cacheLock = new();
    private static readonly HttpClient s_httpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Dogebot/1.0");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ko-KR");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public async Task<string?> GetTodayMarketDigestAsync()
    {
        try
        {
            string? digest;

            lock (s_cacheLock)
            {
                // Check if cache is still valid
                if (s_cachedDigest != null && DateTime.UtcNow - s_lastFetchTime < CacheDuration)
                {
                    logger.LogInformation("[TODAY_MARKET] Using cached market digest (age: {Age}s)", (DateTime.UtcNow - s_lastFetchTime).TotalSeconds);
                    digest = s_cachedDigest;
                }
                else
                {
                    digest = null;
                }
            }

            // Fetch new digest if cache is invalid
            if (digest == null)
            {
                var response = await s_httpClient.GetAsync(TodayMarketDigestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("[TODAY_MARKET] HTTP request failed with status {StatusCode}", (int)response.StatusCode);
                    return null;
                }

                digest = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(digest))
                {
                    logger.LogWarning("[TODAY_MARKET] Empty response from market digest endpoint");
                    return null;
                }

                // Update cache
                lock (s_cacheLock)
                {
                    s_cachedDigest = digest;
                    s_lastFetchTime = DateTime.UtcNow;
                }
            }

            return digest;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[TODAY_MARKET] Error fetching today's market digest");
            return null;
        }
    }

    public DateTime? GetLastCacheTime()
    {
        lock (s_cacheLock)
        {
            return s_lastFetchTime == DateTime.MinValue ? null : s_lastFetchTime;
        }
    }

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_today_market_digest", "Get today's Korean stock market digest as raw JSON from 한국주식데이터(aikstockdata.com). It includes KOSPI/KOSDAQ index close, market breadth, top disclosures, growth rankings, recent earnings, and movers. Use this when the user asks about today's Korean stock market status, index movements, market mood, top disclosures, or recent earnings. The data is a daily snapshot (updated every trading day 18:10 KST) and the quote is the previous trading day's confirmed close — not real-time.", DengAiJsonSchema.Object())
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("get_today_market_digest", StringComparison.Ordinal)) return "Unknown today market digest tool.";

        var digest = await GetTodayMarketDigestAsync();
        if (string.IsNullOrEmpty(digest)) return DengAiToolJson.Serialize(new { Message = "오늘의 시장 다이제스트를 가져오지 못했습니다." });

        return digest;
    }

    #endregion
}
