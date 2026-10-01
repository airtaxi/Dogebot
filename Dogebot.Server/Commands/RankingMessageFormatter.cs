namespace Dogebot.Server.Commands;

/// <summary>
/// Builds the shared chat ranking message text used by the ranking command handlers.
/// </summary>
internal static class RankingMessageFormatter
{
    private const string WordJoiner = "\u2060";

    /// <summary>
    /// Formats a user ranking section with medals, inserting word joiners so sender names do not trigger mentions.
    /// </summary>
    public static string FormatUserRanking(string title, IReadOnlyList<(string SenderName, long MessageCount)> topUsers)
    {
        var message = $"{title}\n\n";

        for (var index = 0; index < topUsers.Count; index++)
        {
            var (senderName, messageCount) = topUsers[index];
            var medal = index switch
            {
                0 => "🥇",
                1 => "🥈",
                2 => "🥉",
                _ => $"{index + 1}."
            };

            message += $"{medal} {InsertWordJoiners(senderName)}: {messageCount:N0}회\n";
        }

        return message.TrimEnd();
    }

    public static string InsertWordJoiners(string value) =>
        value.Length <= 1 ? value : string.Join(WordJoiner, value.Select(character => character.ToString()));
}
