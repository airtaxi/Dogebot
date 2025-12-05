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
        if (content.TrimStart().StartsWith('/') || content.TrimStart().StartsWith('!'))
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
