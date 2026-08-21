using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class UserBaseballTeamPreferenceService : IUserBaseballTeamPreferenceService
{
    private readonly IMongoCollection<UserBaseballTeamPreference> _preferences;
    private readonly ILogger<UserBaseballTeamPreferenceService> _logger;

    public UserBaseballTeamPreferenceService(IMongoDbService mongoDbService, ILogger<UserBaseballTeamPreferenceService> logger)
    {
        _preferences = mongoDbService.Database.GetCollection<UserBaseballTeamPreference>("userBaseballTeamPreferences");
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
}
