using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class MigrationService : IMigrationService
{
    private readonly IMongoCollection<MigrationRecord> _migrations;
    private readonly IMongoCollection<MessageContent> _messageContents;
    private readonly IMongoCollection<WordContent> _wordContents;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(IMongoDbService mongoDbService, ILogger<MigrationService> logger)
    {
        _migrations = mongoDbService.Database.GetCollection<MigrationRecord>("migrations");
        _messageContents = mongoDbService.Database.GetCollection<MessageContent>("messageContents");
        _wordContents = mongoDbService.Database.GetCollection<WordContent>("wordContents");
        _logger = logger;

        // Ensure unique index on version
        var indexKeys = Builders<MigrationRecord>.IndexKeys.Ascending(x => x.Version);
        var indexModel = new CreateIndexModel<MigrationRecord>(indexKeys, new CreateIndexOptions { Unique = true });
        _migrations.Indexes.CreateOne(indexModel);
    }

    public async Task RunMigrationsAsync()
    {
        await ApplyMigrationAsync(1, "SplitMessageContentsToWords", MigrateMessageContentsToWordsAsync);
    }

    private async Task ApplyMigrationAsync(int version, string name, Func<Task> migration)
    {
        var filter = Builders<MigrationRecord>.Filter.Eq(x => x.Version, version);
        var existing = await _migrations.Find(filter).FirstOrDefaultAsync();

        if (existing is not null)
        {
            _logger.LogInformation("[MIGRATION] v{Version} ({Name}) already applied, skipping.", version, name);
            return;
        }

        _logger.LogInformation("[MIGRATION] Applying v{Version} ({Name})...", version, name);
        await migration();

        await _migrations.InsertOneAsync(new MigrationRecord
        {
            Version = version,
            Name = name,
            AppliedAt = DateTime.UtcNow
        });

        _logger.LogInformation("[MIGRATION] v{Version} ({Name}) applied successfully.", version, name);
    }

    /// <summary>
    /// v1: Split existing messageContents into individual words and populate wordContents collection.
    /// </summary>
    private async Task MigrateMessageContentsToWordsAsync()
    {
        var allMessages = await _messageContents.Find(Builders<MessageContent>.Filter.Empty).ToListAsync();
        _logger.LogInformation("[MIGRATION] Processing {Count} message content records...", allMessages.Count);

        var wordAggregation = new Dictionary<(string RoomId, string Word), (long Count, long LastTime)>();

        foreach (var message in allMessages)
        {
            var words = SplitIntoWords(message.Content);
            foreach (var word in words)
            {
                var key = (message.RoomId, word);
                if (wordAggregation.TryGetValue(key, out var existing))
                {
                    wordAggregation[key] = (existing.Count + message.Count, Math.Max(existing.LastTime, message.LastTime));
                }
                else
                {
                    wordAggregation[key] = (message.Count, message.LastTime);
                }
            }
        }

        if (wordAggregation.Count == 0)
        {
            _logger.LogInformation("[MIGRATION] No words to migrate.");
            return;
        }

        var bulkOps = wordAggregation.Select(kvp =>
        {
            var filter = Builders<WordContent>.Filter.And(
                Builders<WordContent>.Filter.Eq(x => x.RoomId, kvp.Key.RoomId),
                Builders<WordContent>.Filter.Eq(x => x.Word, kvp.Key.Word)
            );
            var update = Builders<WordContent>.Update
                .Inc(x => x.Count, kvp.Value.Count)
                .Max(x => x.LastTime, kvp.Value.LastTime);
            return new UpdateOneModel<WordContent>(filter, update) { IsUpsert = true };
        }).ToList();

        const int batchSize = 1000;
        for (int i = 0; i < bulkOps.Count; i += batchSize)
        {
            var batch = bulkOps.Skip(i).Take(batchSize).ToList();
            await _wordContents.BulkWriteAsync(batch);
        }

        _logger.LogInformation("[MIGRATION] Migrated {Count} unique word entries.", wordAggregation.Count);
    }

    internal static string[] SplitIntoWords(string content)
    {
        return content
            .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2)
            .Select(w => w.ToLowerInvariant())
            .Distinct()
            .ToArray();
    }
}
