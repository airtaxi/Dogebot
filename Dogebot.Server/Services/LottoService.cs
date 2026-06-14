using System.Text.Json;

namespace Dogebot.Server.Services;

public class LottoService : ILottoService
{
    private const int MinimumLottoCount = 1;
    private const int MaximumLottoCount = 10;

    private readonly Random _random = new();

    public string CreateLottoMessage(int count = 1)
    {
        var normalizedCount = NormalizeCount(count);
        var lines = new string[normalizedCount];
        for (var index = 0; index < normalizedCount; index++)
        {
            var numbers = Enumerable.Range(1, 45).OrderBy(_ => _random.Next()).Take(6).OrderBy(number => number).ToArray();
            lines[index] = $"{index + 1}회: {string.Join(", ", numbers)}";
        }

        return normalizedCount == 1 ? $"🎱 로또 번호\n{lines[0][4..]}" : $"🎱 로또 번호 ({normalizedCount}회)\n\n{string.Join('\n', lines)}";
    }

    private static int NormalizeCount(int count) =>
        Math.Clamp(count, MinimumLottoCount, MaximumLottoCount);

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("generate_lotto_numbers", "Generate 1 to 10 sets of Korean lotto numbers.", DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["count"] = DengAiJsonSchemaProperty.Integer("Number of lotto number sets to generate. Allowed range is 1 to 10.", MinimumLottoCount, MaximumLottoCount)
        }))
    ];

    Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("generate_lotto_numbers", StringComparison.Ordinal)) return Task.FromResult("Unknown lotto tool.");

        var count = DengAiToolJson.ReadInt32(arguments, "count") ?? 1;
        return Task.FromResult(CreateLottoMessage(count));
    }

    #endregion
}
