namespace KakaoBotAT.Server.Services;

/// <summary>
/// Helper class to filter out non-text messages from statistics.
/// Filters messages like emoticons, photos, deleted messages, etc.
/// </summary>
public static class MessageBlacklist
{
    private static readonly string[] BlacklistPatterns =
    [
        "이모티콘을 보냈습니다.",
        "사진을 보냈습니다.",
        "동영상을 보냈습니다.",
        "사진 2장을 보냈습니다.",
        "사진 3장을 보냈습니다.",
        "사진 4장을 보냈습니다.",
        "사진 5장을 보냈습니다.",
        "사진 6장을 보냈습니다.",
        "사진 7장을 보냈습니다.",
        "사진 8장을 보냈습니다.",
        "사진 9장을 보냈습니다.",
        "사진 11장을 보냈습니다.",
        "사진 12장을 보냈습니다.",
        "사진 13장을 보냈습니다.",
        "사진 14장을 보냈습니다.",
        "사진 15장을 보냈습니다.",
        "사진 16장을 보냈습니다.",
        "사진 17장을 보냈습니다.",
        "사진 18장을 보냈습니다.",
        "사진 19장을 보냈습니다.",
        "사진 20장을 보냈습니다.",
        "사진 21장을 보냈습니다.",
        "사진 22장을 보냈습니다.",
        "사진 23장을 보냈습니다.",
        "사진 24장을 보냈습니다.",
        "사진 25장을 보냈습니다.",
        "사진 26장을 보냈습니다.",
        "사진 27장을 보냈습니다.",
        "사진 28장을 보냈습니다.",
        "사진 29장을 보냈습니다.",
        "사진 30장을 보냈습니다.",
        "(사진)",
        "(동영상)",
        "(파일)",
        "(음성)",
        "(삭제된 메시지입니다)",
        "삭제된 메시지입니다.",
        "(링크)",
        "(지도)",
        "(연락처)",
        "(음악)",
        "확률",
        "샵검색:",
        "샵검색 :",
        "#검색:"
    ];

    /// <summary>
    /// Checks if a message content should be excluded from statistics.
    /// </summary>
    /// <param name="content">The message content to check</param>
    /// <returns>True if the message should be blacklisted (excluded), false otherwise</returns>
    public static bool IsBlacklisted(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return true;

        // Filter out command messages
        if (content.TrimStart().StartsWith('/')
            || content.TrimStart().StartsWith('!')
            || content.TrimStart().StartsWith("심심아")
            || content.TrimStart().StartsWith("판사님")
            || content.TrimStart().StartsWith("소라고동님"))
            return true;

        // Check against blacklist patterns
        foreach (var pattern in BlacklistPatterns)
        {
            if (content.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
