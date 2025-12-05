using KakaoBotAT.Server.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public interface ISimSimService
{
    Task AddResponseAsync(string message, string response, string createdBy);
    Task<bool> DeleteResponseAsync(string message, string response);
    Task<long> DeleteAllResponsesForMessageAsync(string message);
    Task<List<string>> GetResponsesAsync(string message);
    Task<int> GetResponseCountAsync(string message);
    Task<List<(string Message, int Count)>> GetTopMessagesAsync(int limit = 10);
}

public class SimSimService : ISimSimService
{
    private readonly IMongoCollection<SimSimData> _simSimData;

    public SimSimService(IMongoDbService mongoDbService)
    {
        _simSimData = mongoDbService.Database.GetCollection<SimSimData>("simSimData");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<SimSimData>.IndexKeys
            .Ascending(x => x.Message)
            .Ascending(x => x.Response);
        var indexModel = new CreateIndexModel<SimSimData>(indexKeys);
        _simSimData.Indexes.CreateOne(indexModel);
    }

    public async Task AddResponseAsync(string message, string response, string createdBy)
    {
        var normalizedMessage = message.Trim();
        var normalizedResponse = response.Trim();

        // Check if this exact combination already exists
        var filter = Builders<SimSimData>.Filter.And(
            Builders<SimSimData>.Filter.Eq(x => x.Message, normalizedMessage),
            Builders<SimSimData>.Filter.Eq(x => x.Response, normalizedResponse)
        );

        var exists = await _simSimData.Find(filter).AnyAsync();
        if (exists)
            return; // Don't add duplicates

        var data = new SimSimData
        {
            Message = normalizedMessage,
            Response = normalizedResponse,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _simSimData.InsertOneAsync(data);
    }

    public async Task<bool> DeleteResponseAsync(string message, string response)
    {
        var filter = Builders<SimSimData>.Filter.And(
            Builders<SimSimData>.Filter.Eq(x => x.Message, message.Trim()),
            Builders<SimSimData>.Filter.Eq(x => x.Response, response.Trim())
        );

        var result = await _simSimData.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<long> DeleteAllResponsesForMessageAsync(string message)
    {
        var filter = Builders<SimSimData>.Filter.Eq(x => x.Message, message.Trim());
        var result = await _simSimData.DeleteManyAsync(filter);
        return result.DeletedCount;
    }

    public async Task<List<string>> GetResponsesAsync(string message)
    {
        var filter = Builders<SimSimData>.Filter.Eq(x => x.Message, message.Trim());
        var results = await _simSimData.Find(filter).ToListAsync();
        return results.Select(r => r.Response).ToList();
    }

    public async Task<int> GetResponseCountAsync(string message)
    {
        var filter = Builders<SimSimData>.Filter.Eq(x => x.Message, message.Trim());
        return (int)await _simSimData.CountDocumentsAsync(filter);
    }

    public async Task<List<(string Message, int Count)>> GetTopMessagesAsync(int limit = 10)
    {
        var pipeline = new[]
        {
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$message" },
                { "count", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("count", -1)),
            new BsonDocument("$limit", limit)
        };

        var results = await _simSimData.Aggregate<BsonDocument>(pipeline).ToListAsync();
        
        return results.Select(doc => 
            (doc["_id"].AsString, doc["count"].AsInt32)
        ).ToList();
    }
}
