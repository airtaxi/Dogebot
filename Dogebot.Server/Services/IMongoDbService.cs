using MongoDB.Driver;

namespace Dogebot.Server.Services;

public interface IMongoDbService
{
    IMongoDatabase Database { get; }
    IMongoClient Client { get; }
}

