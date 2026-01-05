using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class UserPreferenceService : IUserPreferenceService
{
    private readonly IMongoCollection<UserWeatherPreference> _preferences;
    private readonly ILogger<UserPreferenceService> _logger;

    public UserPreferenceService(IMongoDbService mongoDbService, ILogger<UserPreferenceService> logger)
    {
        _preferences = mongoDbService.Database.GetCollection<UserWeatherPreference>("userWeatherPreferences");
        _logger = logger;

        // Create index on senderHash for faster queries
        var indexKeys = Builders<UserWeatherPreference>.IndexKeys.Ascending(x => x.SenderHash);
        var indexModel = new CreateIndexModel<UserWeatherPreference>(indexKeys);
        _preferences.Indexes.CreateOneAsync(indexModel).ConfigureAwait(false);
    }

    public async Task<string?> GetUserPreferredCityAsync(string senderHash)
    {
        try
        {
            var filter = Builders<UserWeatherPreference>.Filter.Eq(x => x.SenderHash, senderHash);
            var preference = await _preferences.Find(filter).FirstOrDefaultAsync();
            return preference?.CityName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[USER_PREFERENCE] Error getting preferred city for user {SenderHash}", senderHash);
            return null;
        }
    }

    public async Task SetUserPreferredCityAsync(string senderHash, string cityName)
    {
        try
        {
            var filter = Builders<UserWeatherPreference>.Filter.Eq(x => x.SenderHash, senderHash);
            var update = Builders<UserWeatherPreference>.Update
                .Set(x => x.CityName, cityName)
                .Set(x => x.LastUpdated, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            var options = new UpdateOptions { IsUpsert = true };
            await _preferences.UpdateOneAsync(filter, update, options);

            _logger.LogInformation("[USER_PREFERENCE] Updated preferred city for user {SenderHash} to {CityName}", 
                senderHash, cityName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[USER_PREFERENCE] Error setting preferred city for user {SenderHash}", senderHash);
        }
    }
}
