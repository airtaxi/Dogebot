namespace Dogebot.Server.Baseball;

public sealed record BaseballTeamAliasDefinition(string OfficialTeamName, IReadOnlyList<string> SearchAliases);

