using Dogebot.Server.Baseball;
using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public class BaseballTeamAliasService : IBaseballTeamAliasService
{
    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_baseball_team_aliases", "Get the list of KBO baseball teams with official names and their aliases (nicknames). Call this tool FIRST whenever the user asks about a baseball team, game, schedule, or ranking, or mentions a team name or nickname (e.g. 쓱, 랜더스, 꼴데, 꼴쥐, 기아, 엘지, 쥐, 두산, 라이온즈). Use the aliases to resolve the user's team mention to the official team name, then call get_baseball_schedule, get_baseball_game_detail, or other baseball tools with the official name.", DengAiJsonSchema.Object())
    ];

    Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("get_baseball_team_aliases", StringComparison.Ordinal)) return Task.FromResult("Unknown baseball team alias tool.");

        return Task.FromResult(DengAiToolJson.Serialize(BaseballTeamAliasCatalog.TeamAliasDefinitions));
    }

    #endregion
}
