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
        await ApplyMigrationAsync(3, "ManualSenderHashMappings", InsertManualSenderHashMappingsAsync);
        await ApplyMigrationAsync(4, "AddMovieInfoToImaxNotifications", AddMovieInfoToImaxNotificationsAsync);
        await ApplyMigrationAsync(5, "AddSiteInfoToImaxNotifications", AddSiteInfoToImaxNotificationsAsync);
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

            var upsertFilter = Builders<WordContent>.Filter.And(
                Builders<WordContent>.Filter.Eq(x => x.RoomId, group.Key.RoomId),
                Builders<WordContent>.Filter.Eq(x => x.Word, group.Key.NormalizedWord)
            );
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

    /// <summary>
    /// v3: Manually insert RoomMigrationMapping records for users with duplicate senderHash
    /// in room 463056569657145. The hash with higher message count is the old room hash.
    /// </summary>
    private async Task InsertManualSenderHashMappingsAsync()
    {
        const string targetRoomId = "463056569657145";
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var mappings = new List<RoomMigrationMapping>
        {
            new() { TargetRoomId = targetRoomId, SenderName = "조기산", OldSenderHash = "a2e941bb4a9b61816ae616803d633cedd07940fc576b2e9953abe1c8d3fc5e5a", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "ㅎㅂ 👮‍♂️", OldSenderHash = "65ff2338ab17f740e72b6a20267f9ad0f5a995e4719e780ceaeff369a6a05668", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "강보성", OldSenderHash = "b1fb8bfc7c180348e2bd55763c534123a82e97c71b1245800ce3cecfbe25c66e", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "작전과장", OldSenderHash = "874ae5a690c03ec538370006580e1de98a9664c2c50c6a2794a85f0d2dfd1f82", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "니조랄 아옮옮옮", OldSenderHash = "b8a45d25f7b48b9dae58f7234d46bf1d4e6781bb101d68ca2e2f6a408db92136", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "조민석", OldSenderHash = "b2ce10e5d04cede96346b03d46b250b0577c4b44a44121935d38da1c4bb3c65a", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "서", OldSenderHash = "97df0dd6c0f590793e7aa22832a9066afe88d24c246f8ccf19a09a42fe4535f5", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "잼민상", OldSenderHash = "9a126180dc6b9bc7c3c6be4229f0d23cc93a9f23975211acfd50169b0863abbe", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "박현우", OldSenderHash = "702d176ff4c7af6b32af4449b4e42856d9b21dc7df0b7b077e12d113d080a604", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "이호원", OldSenderHash = "e0301c38bb0443a26aa2133d3c888c912dd4fb425c4d424d8871aba7013f06fe", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "양인진", OldSenderHash = "b9e2adb4b904951a76f6f71bb7147952df5a26da02df69d8929e355101ad9ff6", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "이진우", OldSenderHash = "3753d900a057f5b7691d9ddc7ef1d237fc69616fee271ebef0bd26bbd493e9f5", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "강관형(K.Shephard)", OldSenderHash = "746469515926a46ca87cc7cc75e9bd9fa3205fdc727cbe4d5786aa2154ddb599", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "토루", OldSenderHash = "4c99749dc958e64d3fc16612da6d6e961f032b6917c7e71bc4bee129b4614278", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "ms sung", OldSenderHash = "72801b523e07b68a7eabf66734aa91d47f1b6d27a0aa1fca8385c96108bb39ed", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "日本第一歌手皇太子安倍晋三", OldSenderHash = "a5f9422ebc493740c56c69e791ce6f319b40010d0b2a33390a636eb25c62b429", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "버붕이", OldSenderHash = "9011ed40305ebb2adacf47dc16517d89aac1908e93dea1f3cc3fd128a4d866a1", CreatedAt = now },
            new() { TargetRoomId = targetRoomId, SenderName = "開拓者", OldSenderHash = "ec07e77289d0df0b7bfbd689fb7564e80a7eefd503cd8cedafd17f6515841f20", CreatedAt = now },
        };

        var migrationMappings = _database.GetCollection<RoomMigrationMapping>("roomMigrationMappings");

        foreach (var mapping in mappings)
        {
            var existingFilter = Builders<RoomMigrationMapping>.Filter.And(
                Builders<RoomMigrationMapping>.Filter.Eq(x => x.TargetRoomId, mapping.TargetRoomId),
                Builders<RoomMigrationMapping>.Filter.Eq(x => x.SenderName, mapping.SenderName)
            );
            var existing = await migrationMappings.Find(existingFilter).FirstOrDefaultAsync();
            if (existing is not null)
            {
                _logger.LogInformation("[MIGRATION] Mapping for {SenderName} already exists, skipping.", mapping.SenderName);
                continue;
            }

            await migrationMappings.InsertOneAsync(mapping);
        }

        _logger.LogInformation("[MIGRATION] Inserted {Count} manual senderHash mappings for room {RoomId}.",
            mappings.Count, targetRoomId);
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

        var filter = Builders<ImaxNotification>.Filter.Or(
            Builders<ImaxNotification>.Filter.Exists(x => x.MovieName, false),
            Builders<ImaxNotification>.Filter.Eq(x => x.MovieName, string.Empty));

        var update = Builders<ImaxNotification>.Update
            .Set(x => x.MovieName, "프로젝트 헤일메리")
            .Set(x => x.MovieNumber, "30000994");

        var result = await imaxNotifications.UpdateManyAsync(filter, update);

        _logger.LogInformation("[MIGRATION] Updated {Count} IMAX notifications with movie info (프로젝트 헤일메리).",
            result.ModifiedCount);
    }

    /// <summary>
    /// v5: Add siteNumber and siteName fields to existing IMAX notifications.
    /// Existing notifications are assumed to be for 용산아이파크몰 (siteNo=0013).
    /// </summary>
    private async Task AddSiteInfoToImaxNotificationsAsync()
    {
        var imaxNotifications = _database.GetCollection<ImaxNotification>("imaxNotifications");

        var filter = Builders<ImaxNotification>.Filter.Or(
            Builders<ImaxNotification>.Filter.Exists(x => x.SiteNumber, false),
            Builders<ImaxNotification>.Filter.Eq(x => x.SiteNumber, null),
            Builders<ImaxNotification>.Filter.Eq(x => x.SiteNumber, string.Empty));

        var update = Builders<ImaxNotification>.Update
            .Set(x => x.SiteNumber, "0013")
            .Set(x => x.SiteName, "용산아이파크몰");

        var result = await imaxNotifications.UpdateManyAsync(filter, update);

        _logger.LogInformation("[MIGRATION] Updated {Count} IMAX notifications with site info (용산아이파크몰).",
            result.ModifiedCount);
    }
}

