using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class UserBaseballTeamPreferenceService : IUserBaseballTeamPreferenceService
{
    private readonly IMongoCollection<UserBaseballTeamPreference> _preferences;
    private readonly IChatStatisticsService _chatStatisticsService;
    private readonly ILogger<UserBaseballTeamPreferenceService> _logger;

    public UserBaseballTeamPreferenceService(IMongoDbService mongoDbService,  IChatStatisticsService chatStatisticsService, ILogger<UserBaseballTeamPreferenceService> logger)
    {
        _preferences = mongoDbService.Database.GetCollection<UserBaseballTeamPreference>("userBaseballTeamPreferences");
        _chatStatisticsService = chatStatisticsService;
        _logger = logger;
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<UserBaseballTeamPreference>.IndexKeys.Ascending(x => x.SenderHash);
        var indexModel = new CreateIndexModel<UserBaseballTeamPreference>(indexKeys, new CreateIndexOptions { Unique = true });
        _preferences.Indexes.CreateOne(indexModel);
    }

    public async Task<string?> GetUserPreferredTeamAsync(string senderHash)
    {
        try
        {
            var filter = Builders<UserBaseballTeamPreference>.Filter.Eq(x => x.SenderHash, senderHash);
            var preference = await _preferences.Find(filter).FirstOrDefaultAsync();
            return preference?.TeamName;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[USER_BASEBALL_TEAM] Error getting preferred team for user {SenderHash}", senderHash);
            return null;
        }
    }

    public async Task<string?> GetUserPreferredTeamByNameAsync(string roomId, string senderName)
    {
        if (string.IsNullOrWhiteSpace(roomId) || string.IsNullOrWhiteSpace(senderName)) return null;

        try
        {
            // Find all user hashes recorded under the nickname in the same room, preferring the most recently registered team.
            var senderHashes = await _chatStatisticsService.GetSenderHashesByNameAsync(roomId, senderName);
            if (senderHashes.Count == 0) return null;

            var filter = Builders<UserBaseballTeamPreference>.Filter.In(x => x.SenderHash, senderHashes);
            var candidates = await _preferences.Find(filter).SortByDescending(x => x.LastUpdated).ToListAsync();
            return candidates.Count > 0 ? candidates[0].TeamName : null;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[USER_BASEBALL_TEAM] Error getting preferred team for sender name {SenderName}", senderName);
            return null;
        }
    }

    public async Task SetUserPreferredTeamAsync(string senderHash, string teamName)
    {
        try
        {
            var filter = Builders<UserBaseballTeamPreference>.Filter.Eq(x => x.SenderHash, senderHash);
            var update = Builders<UserBaseballTeamPreference>.Update
                .Set(x => x.TeamName, teamName)
                .Set(x => x.LastUpdated, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var options = new UpdateOptions { IsUpsert = true };
            await _preferences.UpdateOneAsync(filter, update, options);

            _logger.LogInformation("[USER_BASEBALL_TEAM] Updated preferred team for user {SenderHash} to {TeamName}", senderHash, teamName);
        }
        catch (Exception exception) { _logger.LogError(exception, "[USER_BASEBALL_TEAM] Error setting preferred team for user {SenderHash}", senderHash); }
    }

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_user_baseball_team",
            "Get the KBO baseball team that a user in the same chat room cheers for. Call this tool when the user asks about their own or another user's baseball team preference, or when baseball conversation context about a specific user is needed. "
            + "The nickname is optional: when omitted, look up the requesting user. When a nickname is provided, use it exactly as it appears in the room. "
            + "Use get_baseball_team_aliases to resolve the returned team name into its aliases when needed.",
            DengAiJsonSchema.Object(
                new Dictionary<string, DengAiJsonSchemaProperty>
                {
                    ["nickname"] = DengAiJsonSchemaProperty.String("Nickname of the user in the same chat room. Omit to look up the requesting user.")
                }))
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("get_user_baseball_team", StringComparison.Ordinal)) return "Unknown baseball team preference tool.";

        var nickname = DengAiToolJson.ReadString(arguments, "nickname");

        if (string.IsNullOrWhiteSpace(nickname) || nickname.Equals(context.SenderName, StringComparison.OrdinalIgnoreCase)) return DengAiToolJson.SerializeOrMessage(await GetUserPreferredTeamAsync(context.SenderHash), "Not found.");

        var teamName = await GetUserPreferredTeamByNameAsync(context.RoomId, nickname);
        return DengAiToolJson.SerializeOrMessage(teamName, "Not found.");
    }

    #endregion
}
