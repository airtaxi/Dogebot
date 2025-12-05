using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class MongoDbService : IMongoDbService
{
    public IMongoDatabase Database { get; }
    public IMongoClient Client { get; }

    public MongoDbService(IConfiguration configuration)
    {
        var dbId = Environment.GetEnvironmentVariable("DB_ID") ?? configuration["MongoDB:UserId"] ?? "admin";
        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? configuration["MongoDB:Password"] ?? "";
        var dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? configuration["MongoDB:Port"] ?? "27017";
        var dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? configuration["MongoDB:Host"] ?? "localhost";
        var databaseName = configuration["MongoDB:Database"] ?? "Dogebot";

        var connectionString = string.IsNullOrEmpty(dbPassword)
            ? $"mongodb://{dbHost}:{dbPort}"
            : $"mongodb://{dbId}:{dbPassword}@{dbHost}:{dbPort}";

        Client = new MongoClient(connectionString);
        Database = Client.GetDatabase(databaseName);
    }
}
