using Dogebot.Server.Models;
using MongoDB.Driver;
using System.Text.Json;

namespace Dogebot.Server.Services;

public class FortuneService : IFortuneService
{
    private static readonly char[] s_koreanInitials =
    [
        'ㄱ', 'ㄴ', 'ㄷ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅅ', 'ㅇ', 'ㅈ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ'
    ];

    private static readonly int[] s_scoreWeights = [5, 12, 28, 40, 15];
    private static readonly Lock s_fortuneDataLock = new();
    private static Dictionary<int, List<FortuneItem>>? s_wealthFortunes;
    private static Dictionary<int, List<FortuneItem>>? s_successFortunes;
    private static Dictionary<int, List<FortuneItem>>? s_loveFortunes;

    private readonly IMongoCollection<DailyFortuneRecord> _dailyFortuneRecords;
    private readonly ILogger<FortuneService> _logger;

    public FortuneService(IMongoDbService mongoDbService, ILogger<FortuneService> logger)
    {
        _dailyFortuneRecords = mongoDbService.Database.GetCollection<DailyFortuneRecord>("dailyFortuneRecords");
        _logger = logger;
        CreateIndexes();
    }

    public async Task<bool> HasDrawnTodayAsync(string senderHash)
    {
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd");
        var filter = Builders<DailyFortuneRecord>.Filter.Eq(record => record.SenderHash, senderHash) &
                     Builders<DailyFortuneRecord>.Filter.Eq(record => record.Date, today);
        return await _dailyFortuneRecords.Find(filter).AnyAsync();
    }

    public async Task RecordDrawAsync(string senderHash)
    {
        var today = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd");
        var record = new DailyFortuneRecord
        {
            SenderHash = senderHash,
            Date = today
        };
        await _dailyFortuneRecords.InsertOneAsync(record);
    }

    public string? CreateFortuneMessage()
    {
        LoadFortuneData();

        if (s_wealthFortunes is not { Count: > 0 } || s_successFortunes is not { Count: > 0 } || s_loveFortunes is not { Count: > 0 }) return null;

        var wealth = PickFortune(s_wealthFortunes);
        var success = PickFortune(s_successFortunes);
        var love = PickFortune(s_loveFortunes);
        var noblePersonInitial1 = s_koreanInitials[Random.Shared.Next(s_koreanInitials.Length)];
        var noblePersonInitial2 = s_koreanInitials[Random.Shared.Next(s_koreanInitials.Length)];

        return $"🔮 오늘의 운세\n\n" +
            $"💰 재물운 {FormatStars(wealth.Score)}\n{wealth.Text}\n\n" +
            $"🏆 성공운 {FormatStars(success.Score)}\n{success.Text}\n\n" +
            $"💕 애정운 {FormatStars(love.Score)}\n{love.Text}\n\n" +
            $"👤 오늘의 귀인: {noblePersonInitial1}{noblePersonInitial2}";
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<DailyFortuneRecord>.IndexKeys
            .Ascending(record => record.SenderHash)
            .Ascending(record => record.Date);
        var indexModel = new CreateIndexModel<DailyFortuneRecord>(indexKeys, new CreateIndexOptions { Unique = true });
        _dailyFortuneRecords.Indexes.CreateOne(indexModel);
    }

    private void LoadFortuneData()
    {
        if (s_wealthFortunes != null) return;

        lock (s_fortuneDataLock)
        {
            if (s_wealthFortunes != null) return;

            try
            {
                var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                s_wealthFortunes = LoadFortuneFile(Path.Combine(assetsPath, "FortuneWealth.json"), options);
                s_successFortunes = LoadFortuneFile(Path.Combine(assetsPath, "FortuneSuccess.json"), options);
                s_loveFortunes = LoadFortuneFile(Path.Combine(assetsPath, "FortuneLove.json"), options);

                _logger.LogInformation("[FORTUNE] Loaded fortunes: Wealth={Wealth}, Success={Success}, Love={Love}", s_wealthFortunes.Values.Sum(list => list.Count), s_successFortunes.Values.Sum(list => list.Count), s_loveFortunes.Values.Sum(list => list.Count));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "[FORTUNE] Error loading fortune data");
                s_wealthFortunes ??= [];
                s_successFortunes ??= [];
                s_loveFortunes ??= [];
            }
        }
    }

    private static Dictionary<int, List<FortuneItem>> LoadFortuneFile(string path, JsonSerializerOptions options)
    {
        if (!File.Exists(path)) return [];

        var json = File.ReadAllText(path);
        var items = JsonSerializer.Deserialize<List<FortuneItem>>(json, options) ?? [];
        return items.GroupBy(item => item.Score).ToDictionary(group => group.Key, group => group.ToList());
    }

    private static string FormatStars(int score) =>
        new string('★', score) + new string('☆', 5 - score);

    private static int PickWeightedScore()
    {
        var roll = Random.Shared.Next(s_scoreWeights.Sum());
        var cumulative = 0;
        for (var index = 0; index < s_scoreWeights.Length; index++)
        {
            cumulative += s_scoreWeights[index];
            if (roll < cumulative) return index + 1;
        }

        return 3;
    }

    private static FortuneItem PickFortune(Dictionary<int, List<FortuneItem>> fortunes)
    {
        var score = PickWeightedScore();
        if (fortunes.TryGetValue(score, out var items) && items.Count > 0) return items[Random.Shared.Next(items.Count)];

        var allItems = fortunes.Values.SelectMany(list => list).ToList();
        return allItems[Random.Shared.Next(allItems.Count)];
    }

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("generate_fortune_preview", "Generate a stateless fortune preview without recording the user's daily fortune draw.", DengAiJsonSchema.Object())
    ];

    Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("generate_fortune_preview", StringComparison.Ordinal)) return Task.FromResult("Unknown fortune tool.");

        return Task.FromResult(CreateFortuneMessage() ?? "운세 데이터를 불러올 수 없습니다.");
    }

    #endregion
}

