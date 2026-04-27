using Dogebot.Commons;
using Dogebot.Server.Models;
using Dogebot.Server.Services;
using System.Text.Json;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !운세 command to display today's fortune.
/// Randomly selects one fortune from each category (wealth, success, love)
/// and generates a random Korean initial pair for "오늘의 귀인".
/// </summary>
public class FortuneCommandHandler(ILogger<FortuneCommandHandler> logger, IFortuneService fortuneService) : ICommandHandler
{
    private static readonly char[] s_koreanInitials =
    [
        'ㄱ', 'ㄴ', 'ㄷ', 'ㄹ', 'ㅁ', 'ㅂ', 'ㅅ', 'ㅇ', 'ㅈ', 'ㅊ', 'ㅋ', 'ㅌ', 'ㅍ', 'ㅎ'
    ];

    // Weighted score distribution: score 1~5 (1=very rare bad, 4=most common, 5=rare good)
    private static readonly int[] s_scoreWeights = [5, 12, 28, 40, 15];

    private static readonly object s_lock = new();
    private static Dictionary<int, List<FortuneItem>>? s_wealthFortunes;
    private static Dictionary<int, List<FortuneItem>>? s_successFortunes;
    private static Dictionary<int, List<FortuneItem>>? s_loveFortunes;

    private readonly Random _random = new();

    public string Command => "!운세";

    public bool CanHandle(string content) =>
        content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (await fortuneService.HasDrawnTodayAsync(data.SenderHash))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "🔮 오늘의 운세는 이미 확인하셨습니다. 내일 다시 시도해주세요!"
                };
            }

            LoadFortuneData();

            if (s_wealthFortunes is not { Count: > 0 } ||
                s_successFortunes is not { Count: > 0 } ||
                s_loveFortunes is not { Count: > 0 })
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 운세 데이터를 불러올 수 없습니다."
                };
            }

            var wealth = PickFortune(s_wealthFortunes);
            var success = PickFortune(s_successFortunes);
            var love = PickFortune(s_loveFortunes);

            var noblePersonInitial1 = s_koreanInitials[_random.Next(s_koreanInitials.Length)];
            var noblePersonInitial2 = s_koreanInitials[_random.Next(s_koreanInitials.Length)];

            var message = $"🔮 오늘의 운세\n\n" +
                $"💰 재물운 {FormatStars(wealth.Score)}\n{wealth.Text}\n\n" +
                $"🏆 성공운 {FormatStars(success.Score)}\n{success.Text}\n\n" +
                $"💕 애정운 {FormatStars(love.Score)}\n{love.Text}\n\n" +
                $"👤 오늘의 귀인: {noblePersonInitial1}{noblePersonInitial2}";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[FORTUNE] Fortune told for {Sender} in room {RoomId}", data.SenderName, data.RoomId);

            await fortuneService.RecordDrawAsync(data.SenderHash);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[FORTUNE] Error processing fortune command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "운세를 확인하는 중 오류가 발생했습니다."
            };
        }
    }

    private static string FormatStars(int score) =>
        new string('★', score) + new string('☆', 5 - score);

    private int PickWeightedScore()
    {
        var roll = _random.Next(s_scoreWeights.Sum());
        var cumulative = 0;
        for (var i = 0; i < s_scoreWeights.Length; i++)
        {
            cumulative += s_scoreWeights[i];
            if (roll < cumulative)
                return i + 1;
        }
        return 3;
    }

    private FortuneItem PickFortune(Dictionary<int, List<FortuneItem>> fortunes)
    {
        var score = PickWeightedScore();
        if (fortunes.TryGetValue(score, out var items) && items.Count > 0)
            return items[_random.Next(items.Count)];

        var allItems = fortunes.Values.SelectMany(list => list).ToList();
        return allItems[_random.Next(allItems.Count)];
    }

    private void LoadFortuneData()
    {
        if (s_wealthFortunes != null)
            return;

        lock (s_lock)
        {
            if (s_wealthFortunes != null)
                return;

            try
            {
                var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets");
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                s_wealthFortunes = LoadFortuneFile(Path.Combine(assetsPath, "FortuneWealth.json"), options);
                s_successFortunes = LoadFortuneFile(Path.Combine(assetsPath, "FortuneSuccess.json"), options);
                s_loveFortunes = LoadFortuneFile(Path.Combine(assetsPath, "FortuneLove.json"), options);

                logger.LogInformation("[FORTUNE] Loaded fortunes: Wealth={Wealth}, Success={Success}, Love={Love}",
                    s_wealthFortunes.Values.Sum(list => list.Count),
                    s_successFortunes.Values.Sum(list => list.Count),
                    s_loveFortunes.Values.Sum(list => list.Count));
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[FORTUNE] Error loading fortune data");
                s_wealthFortunes ??= new Dictionary<int, List<FortuneItem>>();
                s_successFortunes ??= new Dictionary<int, List<FortuneItem>>();
                s_loveFortunes ??= new Dictionary<int, List<FortuneItem>>();
            }
        }
    }

    private static Dictionary<int, List<FortuneItem>> LoadFortuneFile(string path, JsonSerializerOptions options)
    {
        if (!File.Exists(path))
            return new Dictionary<int, List<FortuneItem>>();

        var json = File.ReadAllText(path);
        var items = JsonSerializer.Deserialize<List<FortuneItem>>(json, options) ?? [];
        return items.GroupBy(item => item.Score).ToDictionary(group => group.Key, group => group.ToList());
    }
}

