using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public class OddEvenService : IOddEvenService
{
    private readonly Random _random = new();

    public string PlayOddEven(string userChoice)
    {
        var result = _random.Next(0, 2) == 0 ? "홀" : "짝";
        var isWin = userChoice.Equals(result, StringComparison.OrdinalIgnoreCase);
        return $"🎲 결과: {result}\n{(isWin ? "✅ 맞췄습니다!" : "❌ 틀렸습니다!")}";
    }

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("play_odd_even", "Play a Korean odd/even (홀짝) guessing game. The user picks 홀 (odd) or 짝 (even), the bot randomly draws a result, and reports whether the user won.", DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["choice"] = DengAiJsonSchemaProperty.String("The user's guess: 홀 (odd) or 짝 (even).", ["홀", "짝"])
        }, ["choice"]))
    ];

    Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("play_odd_even", StringComparison.Ordinal)) return Task.FromResult("Unknown odd/even tool.");

        var choice = DengAiToolJson.ReadString(arguments, "choice");
        if (string.IsNullOrWhiteSpace(choice)) return Task.FromResult("홀 또는 짝을 선택해주세요.");

        return Task.FromResult(PlayOddEven(choice));
    }

    #endregion
}
