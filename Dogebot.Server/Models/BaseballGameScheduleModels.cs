namespace Dogebot.Server.Models;

public sealed record BaseballGameScheduleSnapshot(
    DateOnly GameDate,
    IReadOnlyList<BaseballGameScheduleSummary> GameSummaries);

public sealed record BaseballGameScheduleSummary(
    long GameId,
    string GameStatus,
    string GameDetailStatus,
    string PeriodType,
    string CurrentPeriod,
    string StartDate,
    string StartTime,
    BaseballGameParticipant HomeParticipant,
    BaseballGameParticipant AwayParticipant,
    BaseballGameScore? HomeScore,
    BaseballGameScore? AwayScore,
    BaseballGameField? Field);

public sealed record BaseballGameDetail(
    BaseballGameScheduleSummary GameSummary,
    BaseballGamePlayer? HomeStartingPitcher,
    BaseballGamePlayer? AwayStartingPitcher,
    IReadOnlyList<BaseballGamePlayer> HomePlayers,
    IReadOnlyList<BaseballGamePlayer> AwayPlayers,
    BaseballGameLiveData? LiveData);

public sealed record BaseballGameParticipant(
    string Result,
    BaseballGameTeam Team);

public sealed record BaseballGameTeam(
    string ProviderTeamId,
    string Name,
    string ShortName);

public sealed record BaseballGameScore(
    int? Run,
    int? Hit,
    int? Error,
    int? Walks);

public sealed record BaseballGameField(
    string Name,
    string ShortName);

public sealed record BaseballGamePlayer(
    string ProviderPersonId,
    string Name,
    int? BattingOrder,
    string PositionName);

public sealed record BaseballGameLiveData(
    BaseballGameGround? Ground,
    IReadOnlyList<BaseballGameLiveEvent> LiveEvents);

public sealed record BaseballGameGround(
    int? Ball,
    int? Strike,
    int? Out,
    string BaseFirstRunnerProviderPersonId,
    string BaseSecondRunnerProviderPersonId,
    string BaseThirdRunnerProviderPersonId,
    string LastPeriod);

public sealed record BaseballGameLiveEvent(
    string Period,
    string BatterProviderPersonId,
    int? BallCount,
    int? Ball,
    int? Strike,
    int? Speed,
    string PitcherProviderPersonId,
    string Text,
    string PitchKind);
