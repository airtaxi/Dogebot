namespace KakaoBotAT.Server.Models;

public sealed record BaseballTeamStanding(
    int Rank,
    string TeamName,
    int Games,
    int Wins,
    int Losses,
    int Draws,
    decimal WinningPercentage,
    string GamesBehind,
    string RecentTenGames,
    string Streak,
    string HomeRecord,
    string AwayRecord);
