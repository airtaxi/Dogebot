using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public interface IMongoDbService
{
    IMongoDatabase Database { get; }
    IMongoClient Client { get; }
}
