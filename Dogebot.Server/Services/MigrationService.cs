using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class MigrationService : IMigrationService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<MigrationRecord> _migrations;
    private readonly IMongoCollection<MessageContent> _messageContents;
    private readonly IMongoCollection<WordContent> _wordContents;
    private readonly ILogger<MigrationService> _logger;

    public MigrationService(IMongoDbService mongoDbService, ILogger<MigrationService> logger)
    {
        _database = mongoDbService.Database;
        _migrations = _database.GetCollection<MigrationRecord>("migrations");
        _messageContents = _database.GetCollection<MessageContent>("messageContents");
        _wordContents = _database.GetCollection<WordContent>("wordContents");
        _logger = logger;

        // Ensure unique index on version
        var indexKeys = Builders<MigrationRecord>.IndexKeys.Ascending(x => x.Version);
        var indexModel = new CreateIndexModel<MigrationRecord>(indexKeys, new CreateIndexOptions { Unique = true });
        _migrations.Indexes.CreateOne(indexModel);
    }

    public async Task RunMigrationsAsync()
    {
        await ApplyMigrationAsync(1, "SplitMessageContentsToWords", MigrateMessageContentsToWordsAsync);
        await ApplyMigrationAsync(2, "NormalizeKoreanConsonantWords", NormalizeKoreanConsonantWordsAsync);
        await ApplyMigrationAsync(4, "AddMovieInfoToImaxNotifications", AddMovieInfoToImaxNotificationsAsync);
        await ApplyMigrationAsync(5, "AddSiteInfoToImaxNotifications", AddSiteInfoToImaxNotificationsAsync);
        await ApplyMigrationAsync(6, "AllowMultipleImaxNotificationsPerRoom", AllowMultipleImaxNotificationsPerRoomAsync);
        await ApplyMigrationAsync(7, "RemoveHolidayMonthRecordsWithNullId", RemoveHolidayMonthRecordsWithNullIdAsync);
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
            var filter = Builders<WordContent>.Filter.And(Builders<WordContent>.Filter.Eq(x => x.RoomId, kvp.Key.RoomId), Builders<WordContent>.Filter.Eq(x => x.Word, kvp.Key.Word));
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

    /// <summary>
    /// v2: Normalize repeated Korean consonant words (e.g., ㅋㅋ, ㅋㅋㅋㅋ → ㅋㅋㅋ) and merge counts.
    /// </summary>
    private async Task NormalizeKoreanConsonantWordsAsync()
    {
        var allWords = await _wordContents.Find(Builders<WordContent>.Filter.Empty).ToListAsync();
        _logger.LogInformation("[MIGRATION] Processing {Count} word content records for consonant normalization...", allWords.Count);

        var wordsToNormalize = allWords
            .Where(w => IsRepeatedKoreanConsonant(w.Word) && w.Word != NormalizeKoreanConsonant(w.Word))
            .ToList();

        if (wordsToNormalize.Count == 0)
        {
            _logger.LogInformation("[MIGRATION] No Korean consonant words to normalize.");
            return;
        }

        // Group by (RoomId, NormalizedWord) to merge counts
        var mergeGroups = wordsToNormalize
            .GroupBy(w => (w.RoomId, NormalizedWord: NormalizeKoreanConsonant(w.Word)))
            .ToList();

        var bulkOps = new List<WriteModel<WordContent>>();

        // Delete old un-normalized entries
        foreach (var word in wordsToNormalize)
        {
            var deleteFilter = Builders<WordContent>.Filter.Eq(x => x.Id, word.Id);
            bulkOps.Add(new DeleteOneModel<WordContent>(deleteFilter));
        }

        // Upsert merged counts into normalized entries
        foreach (var group in mergeGroups)
        {
            var totalCount = group.Sum(w => w.Count);
            var maxLastTime = group.Max(w => w.LastTime);

            var upsertFilter = Builders<WordContent>.Filter.And(Builders<WordContent>.Filter.Eq(x => x.RoomId, group.Key.RoomId), Builders<WordContent>.Filter.Eq(x => x.Word, group.Key.NormalizedWord));
            var upsertUpdate = Builders<WordContent>.Update
                .Inc(x => x.Count, totalCount)
                .Max(x => x.LastTime, maxLastTime);
            bulkOps.Add(new UpdateOneModel<WordContent>(upsertFilter, upsertUpdate) { IsUpsert = true });
        }

        const int batchSize = 1000;
        for (int i = 0; i < bulkOps.Count; i += batchSize)
        {
            var batch = bulkOps.Skip(i).Take(batchSize).ToList();
            await _wordContents.BulkWriteAsync(batch);
        }

        _logger.LogInformation("[MIGRATION] Normalized {Count} Korean consonant word entries.", wordsToNormalize.Count);
    }

    private static bool IsRepeatedKoreanConsonant(string word)
    {
        return word.Length > 0 && word[0] is >= 'ㄱ' and <= 'ㅎ' && word.AsSpan().IndexOfAnyExcept(word[0]) == -1;
    }

    private static string NormalizeKoreanConsonant(string word)
    {
        if (IsRepeatedKoreanConsonant(word))
            return new string(word[0], 3);
        return word;
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

    /// <summary>
    /// v4: Add movieName and movieNumber fields to existing IMAX notifications.
    /// Existing notifications are assumed to be for "프로젝트 헤일메리" (movNo=30000994).
    /// </summary>
    private async Task AddMovieInfoToImaxNotificationsAsync()
    {
        var imaxNotifications = _database.GetCollection<ImaxNotification>("imaxNotifications");

        var filter = Builders<ImaxNotification>.Filter.Or(Builders<ImaxNotification>.Filter.Exists(x => x.MovieName, false), Builders<ImaxNotification>.Filter.Eq(x => x.MovieName, string.Empty));

        var update = Builders<ImaxNotification>.Update
            .Set(x => x.MovieName, "프로젝트 헤일메리")
            .Set(x => x.MovieNumber, "30000994");

        var result = await imaxNotifications.UpdateManyAsync(filter, update);

        _logger.LogInformation("[MIGRATION] Updated {Count} IMAX notifications with movie info (프로젝트 헤일메리).", result.ModifiedCount);
    }

    /// <summary>
    /// v5: Add siteNumber and siteName fields to existing IMAX notifications.
    /// Existing notifications are assumed to be for 용산아이파크몰 (siteNo=0013).
    /// </summary>
    private async Task AddSiteInfoToImaxNotificationsAsync()
    {
        var imaxNotifications = _database.GetCollection<ImaxNotification>("imaxNotifications");

        var filter = Builders<ImaxNotification>.Filter.Or(Builders<ImaxNotification>.Filter.Exists(x => x.SiteNumber, false), Builders<ImaxNotification>.Filter.Eq(x => x.SiteNumber, null), Builders<ImaxNotification>.Filter.Eq(x => x.SiteNumber, string.Empty));

        var update = Builders<ImaxNotification>.Update
            .Set(x => x.SiteNumber, "0013")
            .Set(x => x.SiteName, "용산아이파크몰");

        var result = await imaxNotifications.UpdateManyAsync(filter, update);

        _logger.LogInformation("[MIGRATION] Updated {Count} IMAX notifications with site info (용산아이파크몰).", result.ModifiedCount);
    }

    /// <summary>
    /// v6: Allow multiple IMAX notifications per room.
    /// Drops the unique roomId_1 index and creates a compound unique index
    /// (roomId + screeningDate + movieNumber + siteNumber) that enforces the RegisterAsync duplicate rule.
    /// Existing data cannot violate the new index because the old unique roomId_1 index
    /// guaranteed at most one notification per room.
    /// </summary>
    private async Task AllowMultipleImaxNotificationsPerRoomAsync()
    {
        try
        {
            await _database.GetCollection<ImaxNotification>("imaxNotifications").Indexes.DropOneAsync("roomId_1");
            _logger.LogInformation("[MIGRATION] Dropped unique roomId_1 index on imaxNotifications.");
        }
        catch (MongoCommandException exception) when (exception.CodeName is "IndexNotFound" or "NamespaceNotFound")
        {
            _logger.LogInformation("[MIGRATION] roomId_1 index on imaxNotifications does not exist, skipping.");
        }

        var indexKeys = Builders<ImaxNotification>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.ScreeningDate)
            .Ascending(x => x.MovieNumber)
            .Ascending(x => x.SiteNumber);
        var indexModel = new CreateIndexModel<ImaxNotification>(indexKeys, new CreateIndexOptions { Unique = true, Name = "roomId_1_screeningDate_1_movieNumber_1_siteNumber_1" });
        await _database.GetCollection<ImaxNotification>("imaxNotifications").Indexes.CreateOneAsync(indexModel, new CreateOneIndexOptions());
        _logger.LogInformation("[MIGRATION] Created compound unique index on imaxNotifications (roomId + screeningDate + movieNumber + siteNumber).");
    }

    /// <summary>
    /// v7: Remove documents with a null _id from the holidayMonths collection.
    /// (Cleanup of legacy documents stored with _id: null because no ObjectId was generated on upsert insert.)
    /// </summary>
    private async Task RemoveHolidayMonthRecordsWithNullIdAsync()
    {
        var holidayMonthRecords = _database.GetCollection<HolidayMonthRecord>("holidayMonths");
        var filter = Builders<HolidayMonthRecord>.Filter.Eq(record => record.Id, null);
        var result = await holidayMonthRecords.DeleteManyAsync(filter);
        _logger.LogInformation("[MIGRATION] Removed {Count} holiday month records with null _id.", result.DeletedCount);
    }
}

