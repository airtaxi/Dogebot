using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dogebot.Server.Models;
using KoreanNumberParser;

namespace Dogebot.Server.Services;

public partial class UnitConversionService : IUnitConversionService
{
    private static readonly UnitDefinition s_squareMeterUnit = new("제곱미터", UnitCategory.Area, true, 1m, 0m, ["제곱미터", "평방미터", "m2", "m²", "sqm", "㎡"]);
    private static readonly UnitDefinition s_pyeongUnit = new("평", UnitCategory.Area, false, 400m / 121m, 0m, ["평", "pyeong", "py"]);
    private static readonly UnitDefinition s_squareFootUnit = new("제곱피트", UnitCategory.Area, false, 0.09290304m, 0m, ["제곱피트", "스퀘어피트", "ft2", "ft²", "sqft"]);
    private static readonly UnitDefinition s_squareYardUnit = new("제곱야드", UnitCategory.Area, false, 0.83612736m, 0m, ["제곱야드", "스퀘어야드", "yd2", "yd²", "sqyd"]);
    private static readonly UnitDefinition s_squareInchUnit = new("제곱인치", UnitCategory.Area, false, 0.00064516m, 0m, ["제곱인치", "스퀘어인치", "in2", "in²", "sqin"]);
    private static readonly UnitDefinition s_acreUnit = new("에이커", UnitCategory.Area, false, 4046.8564224m, 0m, ["에이커", "acre", "ac"]);
    private static readonly UnitDefinition s_yardUnit = new("야드", UnitCategory.Length, false, 0.9144m, 0m, ["야드", "yard", "yd"]);
    private static readonly UnitDefinition s_quartUnit = new("쿼트", UnitCategory.Volume, false, 0.946352946m, 0m, ["쿼트", "quart", "qt"]);
    private static readonly UnitDefinition s_pintUnit = new("파인트", UnitCategory.Volume, false, 0.473176473m, 0m, ["파인트", "pint", "pt"]);
    private static readonly UnitDefinition s_cupUnit = new("컵", UnitCategory.Volume, false, 0.2365882365m, 0m, ["컵", "cup"]);
    private static readonly UnitDefinition s_shortTonUnit = new("미국톤", UnitCategory.Mass, false, 907184.74m, 0m, ["미국톤", "shortton", "uston"]);
    private static readonly UnitDefinition s_geunUnit = new("근", UnitCategory.Mass, false, 600m, 0m, ["근"]);
    private static readonly UnitDefinition s_gwanUnit = new("관", UnitCategory.Mass, false, 3750m, 0m, ["관"]);
    private static readonly UnitDefinition s_donUnit = new("돈", UnitCategory.Mass, false, 3.75m, 0m, ["돈"]);
    private static readonly UnitDefinition s_celsiusUnit = new("°C", UnitCategory.Temperature, true, 1m, 0m, ["섭씨", "도", "c", "celsius", "℃", "°c"]);
    private static readonly UnitDefinition s_fahrenheitUnit = new("°F", UnitCategory.Temperature, false, 5m / 9m, -160m / 9m, ["화씨", "f", "fahrenheit", "℉", "°f"]);
    private static readonly UnitDefinition s_kelvinUnit = new("K", UnitCategory.Temperature, true, 1m, -273.15m, ["켈빈", "k", "kelvin"]);
    private static readonly UnitDefinition s_kilometersPerHourUnit = new("km/h", UnitCategory.Speed, true, 1m, 0m, ["kmh", "kph", "km/h"]);
    private static readonly UnitDefinition s_milesPerHourUnit = new("mph", UnitCategory.Speed, false, 1.609344m, 0m, ["mph"]);
    private static readonly UnitDefinition s_metersPerSecondUnit = new("m/s", UnitCategory.Speed, true, 3.6m, 0m, ["m/s", "mps", "미터퍼초"]);
    private static readonly UnitDefinition s_knotUnit = new("노트", UnitCategory.Speed, false, 1.852m, 0m, ["노트", "knot", "kt"]);
    private static readonly UnitDefinition s_byteUnit = new("바이트", UnitCategory.Data, true, 1m, 0m, ["바이트", "b"]);

    private static readonly UnitDefinition[] s_lengthMetricUnits =
    [
        new("킬로미터", UnitCategory.Length, true, 1000m, 0m, ["킬로미터", "km"]),
        new("미터", UnitCategory.Length, true, 1m, 0m, ["미터", "메터", "m"]),
        new("센티미터", UnitCategory.Length, true, 0.01m, 0m, ["센티미터", "센치미터", "센치", "cm"]),
        new("밀리미터", UnitCategory.Length, true, 0.001m, 0m, ["밀리미터", "mm"])
    ];

    private static readonly UnitDefinition[] s_lengthImperialUnits =
    [
        new("마일", UnitCategory.Length, false, 1609.344m, 0m, ["마일", "mile", "mi"]),
        new("피트", UnitCategory.Length, false, 0.3048m, 0m, ["피트", "feet", "foot", "ft"]),
        new("인치", UnitCategory.Length, false, 0.0254m, 0m, ["인치", "inch", "in"])
    ];

    private static readonly UnitDefinition[] s_areaMetricUnits =
    [
        new("제곱킬로미터", UnitCategory.Area, true, 1000000m, 0m, ["제곱킬로미터", "평방킬로미터", "km2", "km²", "sqkm"]),
        s_squareMeterUnit,
        new("제곱센티미터", UnitCategory.Area, true, 0.0001m, 0m, ["제곱센티미터", "평방센티미터", "cm2", "cm²", "sqcm"]),
        new("제곱밀리미터", UnitCategory.Area, true, 0.000001m, 0m, ["제곱밀리미터", "평방밀리미터", "mm2", "mm²", "sqmm"])
    ];

    private static readonly UnitDefinition[] s_volumeMetricUnits =
    [
        new("세제곱미터", UnitCategory.Volume, true, 1000m, 0m, ["세제곱미터", "입방미터", "m3", "m³"]),
        new("리터", UnitCategory.Volume, true, 1m, 0m, ["리터", "l", "ℓ", "liter", "litre"]),
        new("밀리리터", UnitCategory.Volume, true, 0.001m, 0m, ["밀리리터", "ml", "cc", "시시"])
    ];

    private static readonly UnitDefinition[] s_volumeImperialUnits =
    [
        new("갤런", UnitCategory.Volume, false, 3.785411784m, 0m, ["갤런", "gallon", "gal"]),
        new("액량온스", UnitCategory.Volume, false, 0.0295735295625m, 0m, ["액량온스", "floz"])
    ];

    private static readonly UnitDefinition[] s_massMetricUnits =
    [
        new("톤", UnitCategory.Mass, true, 1000000m, 0m, ["톤", "t", "tonne"]),
        new("킬로그램", UnitCategory.Mass, true, 1000m, 0m, ["킬로그램", "kg"]),
        new("그램", UnitCategory.Mass, true, 1m, 0m, ["그램", "g", "gram"]),
        new("밀리그램", UnitCategory.Mass, true, 0.001m, 0m, ["밀리그램", "mg"])
    ];

    private static readonly UnitDefinition[] s_massImperialUnits =
    [
        new("파운드", UnitCategory.Mass, false, 453.59237m, 0m, ["파운드", "pound", "lb"]),
        new("온스", UnitCategory.Mass, false, 28.349523125m, 0m, ["온스", "온즈", "ounce", "oz"])
    ];

    private static readonly UnitDefinition[] s_temperatureUnits = [s_celsiusUnit, s_fahrenheitUnit, s_kelvinUnit];

    private static readonly UnitDefinition[] s_speedUnits = [s_kilometersPerHourUnit, s_milesPerHourUnit, s_metersPerSecondUnit, s_knotUnit];

    private static readonly UnitDefinition[] s_dataDecimalUnits =
    [
        new("요타바이트", UnitCategory.Data, true, 1000000000000000000000000m, 0m, ["요타바이트", "요타", "yb"]),
        new("제타바이트", UnitCategory.Data, true, 1000000000000000000000m, 0m, ["제타바이트", "제타", "zb"]),
        new("엑사바이트", UnitCategory.Data, true, 1000000000000000000m, 0m, ["엑사바이트", "엑사", "eb"]),
        new("페타바이트", UnitCategory.Data, true, 1000000000000000m, 0m, ["페타바이트", "페타", "pb"]),
        new("테라바이트", UnitCategory.Data, true, 1000000000000m, 0m, ["테라바이트", "테라", "tb"]),
        new("기가바이트", UnitCategory.Data, true, 1000000000m, 0m, ["기가바이트", "기가", "gb"]),
        new("메가바이트", UnitCategory.Data, true, 1000000m, 0m, ["메가바이트", "메가", "mb"]),
        new("킬로바이트", UnitCategory.Data, true, 1000m, 0m, ["킬로바이트", "킬로", "kb"]),
        s_byteUnit
    ];

    private static readonly UnitDefinition[] s_dataBinaryUnits =
    [
        new("요비바이트", UnitCategory.Data, false, 1208925819614629174706176m, 0m, ["요비바이트", "요비", "yib"]),
        new("제비바이트", UnitCategory.Data, false, 1180591620717411303424m, 0m, ["제비바이트", "제비", "zib"]),
        new("엑스비바이트", UnitCategory.Data, false, 1152921504606846976m, 0m, ["엑스비바이트", "엑스비", "eib"]),
        new("페비바이트", UnitCategory.Data, false, 1125899906842624m, 0m, ["페비바이트", "페비", "pib"]),
        new("테비바이트", UnitCategory.Data, false, 1099511627776m, 0m, ["테비바이트", "테비", "tib"]),
        new("기비바이트", UnitCategory.Data, false, 1073741824m, 0m, ["기비바이트", "기비", "gib"]),
        new("메비바이트", UnitCategory.Data, false, 1048576m, 0m, ["메비바이트", "메비", "mib"]),
        new("키비바이트", UnitCategory.Data, false, 1024m, 0m, ["키비바이트", "키비", "kib"])
    ];

    private static readonly UnitDefinition[] s_bitDecimalUnits =
    [
        new("요타비트", UnitCategory.Data, true, 125000000000000000000000m, 0m, ["요타비트", "ybit"]),
        new("제타비트", UnitCategory.Data, true, 125000000000000000000m, 0m, ["제타비트", "zbit"]),
        new("엑사비트", UnitCategory.Data, true, 125000000000000000m, 0m, ["엑사비트", "ebit"]),
        new("페타비트", UnitCategory.Data, true, 125000000000000m, 0m, ["페타비트", "pbit"]),
        new("테라비트", UnitCategory.Data, true, 125000000000m, 0m, ["테라비트", "tbit"]),
        new("기가비트", UnitCategory.Data, true, 125000000m, 0m, ["기가비트", "gbit"]),
        new("메가비트", UnitCategory.Data, true, 125000m, 0m, ["메가비트", "mbit"]),
        new("킬로비트", UnitCategory.Data, true, 125m, 0m, ["킬로비트", "kbit"]),
        new("비트", UnitCategory.Data, true, 0.125m, 0m, ["비트", "bit"])
    ];

    private static readonly UnitDefinition[] s_bitBinaryUnits =
    [
        new("요비비트", UnitCategory.Data, false, 151115727451828646838272m, 0m, ["요비비트", "yibit"]),
        new("제비비트", UnitCategory.Data, false, 147573952589676412928m, 0m, ["제비비트", "zibit"]),
        new("엑스비비트", UnitCategory.Data, false, 144115188075855872m, 0m, ["엑스비비트", "eibit"]),
        new("페비비트", UnitCategory.Data, false, 140737488355328m, 0m, ["페비비트", "pibit"]),
        new("테비비트", UnitCategory.Data, false, 137438953472m, 0m, ["테비비트", "tibit"]),
        new("기비비트", UnitCategory.Data, false, 134217728m, 0m, ["기비비트", "gibit"]),
        new("메비비트", UnitCategory.Data, false, 131072m, 0m, ["메비비트", "mibit"]),
        new("키비비트", UnitCategory.Data, false, 128m, 0m, ["키비비트", "kibit"])
    ];

    private static readonly UnitDefinition[] s_allUnits =
    [
        .. s_lengthMetricUnits,
        .. s_lengthImperialUnits,
        s_yardUnit,
        .. s_areaMetricUnits,
        s_pyeongUnit,
        s_squareFootUnit,
        s_squareYardUnit,
        s_squareInchUnit,
        s_acreUnit,
        .. s_volumeMetricUnits,
        .. s_volumeImperialUnits,
        s_quartUnit,
        s_pintUnit,
        s_cupUnit,
        .. s_massMetricUnits,
        .. s_massImperialUnits,
        s_shortTonUnit,
        s_geunUnit,
        s_gwanUnit,
        s_donUnit,
        .. s_temperatureUnits,
        .. s_speedUnits,
        .. s_dataDecimalUnits,
        .. s_dataBinaryUnits,
        .. s_bitDecimalUnits,
        .. s_bitBinaryUnits
    ];

    private static readonly Dictionary<string, UnitDefinition> s_unitAliasMap = CreateUnitAliasMap();

    public Task<string> CreateUnitConversionMessageAsync(string queryText) => Task.FromResult(CreateUnitConversionMessage(queryText));

    private static string CreateUnitConversionMessage(string queryText)
    {
        var parseResult = ParseRequest(queryText);
        if (parseResult.Message is not null) return parseResult.Message;

        try { return CreateConversionResult(parseResult.Request!); }
        catch (OverflowException) { return CreateOverflowMessage(); }
    }

    private static string CreateConversionResult(UnitConversionRequest request)
    {
        if (request.TargetUnitQuery is not null)
        {
            if (!TryResolveUnit(request.TargetUnitQuery, out var targetUnit)) return CreateUnitNotFoundMessage(request.TargetUnitQuery);
            if (targetUnit.Category != request.SourceUnit.Category) return CreateCategoryMismatchMessage(request.SourceUnit, targetUnit);
            return BuildConversionMessage(request.Amount, request.SourceUnit, ConvertToUnit(request.Amount, request.SourceUnit, targetUnit), targetUnit);
        }

        return CreateAutoConversionMessage(request);
    }

    private static string CreateOverflowMessage() => "변환 결과가 너무 커서 처리할 수 없습니다.\n더 작은 단위를 입력해 주세요.";

    private static UnitConversionParseResult ParseRequest(string queryText)
    {
        var trimmedQueryText = queryText.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQueryText)) return new UnitConversionParseResult(null, CreateUsageMessage());

        var tokens = WhitespaceRegex().Split(trimmedQueryText).Where(token => token.Length > 0).ToArray();
        if (tokens.Length is 0) return new UnitConversionParseResult(null, CreateUsageMessage());
        if (tokens.Length > 3) return new UnitConversionParseResult(null, CreateUsageMessage());

        decimal amount;
        UnitDefinition sourceUnit;
        string? targetUnitQuery;

        if (TryResolveUnit(tokens[0], out var firstTokenUnit))
        {
            if (tokens.Length > 2) return new UnitConversionParseResult(null, CreateUsageMessage());

            amount = 1m;
            sourceUnit = firstTokenUnit;
            targetUnitQuery = tokens.Length > 1 ? tokens[1] : null;
        }
        else if (TrySplitAmountAndUnit(tokens[0], out var attachedAmount, out var attachedUnit))
        {
            if (tokens.Length > 2) return new UnitConversionParseResult(null, CreateUsageMessage());

            amount = attachedAmount;
            sourceUnit = attachedUnit;
            targetUnitQuery = tokens.Length > 1 ? tokens[1] : null;
        }
        else if (tokens.Length >= 2 && KoreanNumber.TryParseDecimal(tokens[0], out var separatedAmount) && TryResolveUnit(tokens[1], out var separatedUnit))
        {
            amount = separatedAmount;
            sourceUnit = separatedUnit;
            targetUnitQuery = tokens.Length > 2 ? tokens[2] : null;
        }
        else
        {
            return new UnitConversionParseResult(null, CreateUsageMessage());
        }

        return new UnitConversionParseResult(new UnitConversionRequest(amount, sourceUnit, targetUnitQuery), null);
    }

    private static bool TrySplitAmountAndUnit(string token, out decimal amount, out UnitDefinition unit)
    {
        amount = 0m;
        unit = null!;

        for (var length = token.Length; length > 0; length--)
        {
            var amountText = token[..length];
            if (!KoreanNumber.TryParseDecimal(amountText, out var parsedAmount)) continue;

            var unitQuery = token[length..].Trim();
            if (unitQuery.Length == 0) return false;

            if (TryResolveUnit(unitQuery, out var resolvedUnit))
            {
                amount = parsedAmount;
                unit = resolvedUnit;
                return true;
            }
        }

        return false;
    }

    private static bool TryResolveUnit(string unitQuery, out UnitDefinition unit)
    {
        var normalizedUnitQuery = NormalizeUnitSearchText(unitQuery);
        if (s_unitAliasMap.TryGetValue(normalizedUnitQuery, out var resolvedUnit))
        {
            unit = resolvedUnit;
            return true;
        }

        unit = null!;
        return false;
    }

    private static UnitDefinition[] GetAutoChain(UnitDefinition sourceUnit) => sourceUnit.Category switch
    {
        UnitCategory.Length => sourceUnit.IsMetric ? s_lengthImperialUnits : s_lengthMetricUnits,
        UnitCategory.Volume => sourceUnit.IsMetric ? s_volumeImperialUnits : s_volumeMetricUnits,
        UnitCategory.Mass => sourceUnit.IsMetric ? s_massImperialUnits : s_massMetricUnits,
        UnitCategory.Area => s_areaMetricUnits,
        _ => throw new ArgumentOutOfRangeException(nameof(sourceUnit), sourceUnit, "The unit category is not supported for automatic conversion.")
    };

    private static UnitDefinition SelectAutoUnit(UnitDefinition[] chain, decimal baseValue)
    {
        foreach (var unit in chain)
        {
            if (ConvertFromBase(unit, baseValue) >= 1m)
            {
                return unit;
            }
        }

        return chain[^1];
    }

    private static string CreateAutoConversionMessage(UnitConversionRequest request)
    {
        var sourceUnit = request.SourceUnit;
        var baseValue = ConvertToBase(request.Amount, sourceUnit);

        if (sourceUnit.Category == UnitCategory.Area && sourceUnit == s_squareMeterUnit) return BuildConversionMessage(request.Amount, sourceUnit, ConvertFromBase(s_pyeongUnit, baseValue), s_pyeongUnit);

        if (sourceUnit.Category == UnitCategory.Temperature)
        {
            if (sourceUnit == s_kelvinUnit) return CreateKelvinTargetRequiredMessage();

            var targetUnit = sourceUnit.IsMetric ? s_fahrenheitUnit : s_celsiusUnit;
            return BuildConversionMessage(request.Amount, sourceUnit, ConvertFromBase(targetUnit, baseValue), targetUnit);
        }

        if (sourceUnit.Category == UnitCategory.Speed)
        {
            var targetUnit = sourceUnit.IsMetric ? s_milesPerHourUnit : s_kilometersPerHourUnit;
            return BuildConversionMessage(request.Amount, sourceUnit, ConvertFromBase(targetUnit, baseValue), targetUnit);
        }

        if (sourceUnit.Category == UnitCategory.Data)
        {
            var isBitUnit = s_bitDecimalUnits.Contains(sourceUnit) || s_bitBinaryUnits.Contains(sourceUnit);
            var decimalUnits = isBitUnit ? s_bitDecimalUnits : s_dataDecimalUnits;
            var binaryUnits = isBitUnit ? s_bitBinaryUnits : s_dataBinaryUnits;
            var decimalUnit = SelectAutoUnit(decimalUnits, baseValue);
            var binaryUnit = SelectAutoUnit(binaryUnits, baseValue);
            var primaryUnit = sourceUnit.IsMetric ? decimalUnit : binaryUnit;
            var secondaryUnit = primaryUnit == decimalUnit ? binaryUnit : decimalUnit;

            if (primaryUnit == sourceUnit) return BuildConversionMessage(request.Amount, sourceUnit, ConvertFromBase(secondaryUnit, baseValue), secondaryUnit);

            return BuildMultiConversionMessage(request.Amount, sourceUnit, ConvertFromBase(primaryUnit, baseValue), primaryUnit, ConvertFromBase(secondaryUnit, baseValue), secondaryUnit);
        }

        var selectedUnit = SelectAutoUnit(GetAutoChain(sourceUnit), baseValue);
        return BuildConversionMessage(request.Amount, sourceUnit, ConvertFromBase(selectedUnit, baseValue), selectedUnit);
    }

    private static string BuildConversionMessage(decimal amount, UnitDefinition sourceUnit, decimal targetValue, UnitDefinition targetUnit)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("📏 단위 변환");
        stringBuilder.AppendLine();
        stringBuilder.Append($"{FormatAmount(amount)}{sourceUnit.DisplayName} = {FormatAmount(targetValue)}{targetUnit.DisplayName}");
        return stringBuilder.ToString().TrimEnd();
    }

    private static string BuildMultiConversionMessage(decimal amount, UnitDefinition sourceUnit, decimal primaryValue, UnitDefinition primaryUnit, decimal secondaryValue, UnitDefinition secondaryUnit)
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("📏 단위 변환");
        stringBuilder.AppendLine();
        stringBuilder.Append($"{FormatAmount(amount)}{sourceUnit.DisplayName} = {FormatAmount(primaryValue)}{primaryUnit.DisplayName} = {FormatAmount(secondaryValue)}{secondaryUnit.DisplayName}");
        return stringBuilder.ToString().TrimEnd();
    }

    private static string FormatAmount(decimal value)
    {
        var roundedValue = decimal.Round(value, 4, MidpointRounding.AwayFromZero);
        if (roundedValue == decimal.Truncate(roundedValue)) return roundedValue.ToString("N0", CultureInfo.InvariantCulture);
        return roundedValue.ToString("N4", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
    }

    private static string CreateUsageMessage() =>
        "사용법: !단위 [수치+단위] [목적지 단위]\n" +
        "수치는 단위에 붙여 쓰거나 띄어 쓸 수 있습니다.\n" +
        "목적지 단위를 생략하면 적절한 단위로 자동 변환됩니다.\n" +
        "예시: !단위 100피트 미터, !단위 1.5킬로미터, !단위 1인치, !단위 25도 화씨, !단위 1기가";

    private static string CreateUnitNotFoundMessage(string unitQuery) =>
        $"'{unitQuery}' 단위를 찾지 못했습니다.\n{CreateUsageMessage()}";

    private static string CreateCategoryMismatchMessage(UnitDefinition sourceUnit, UnitDefinition targetUnit) =>
        $"'{sourceUnit.DisplayName}'은(는) {GetCategoryDisplayName(sourceUnit.Category)} 단위입니다.\n'{targetUnit.DisplayName}'은(는) {GetCategoryDisplayName(targetUnit.Category)} 단위라 변환할 수 없습니다.\n{CreateUsageMessage()}";

    private static string CreateKelvinTargetRequiredMessage() =>
        "켈빈은 목적지 단위를 지정해야 합니다.\n예시: !단위 300켈빈 섭씨";

    private static string GetCategoryDisplayName(UnitCategory category) => category switch
    {
        UnitCategory.Length => "길이",
        UnitCategory.Area => "넓이",
        UnitCategory.Volume => "부피",
        UnitCategory.Mass => "무게",
        UnitCategory.Temperature => "온도",
        UnitCategory.Speed => "속도",
        UnitCategory.Data => "데이터 용량",
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "The unit category is not supported.")
    };

    private static decimal ConvertToBase(decimal value, UnitDefinition unit) => value * unit.Factor + unit.Offset;

    private static decimal ConvertFromBase(UnitDefinition unit, decimal baseValue) => (baseValue - unit.Offset) / unit.Factor;

    private static decimal ConvertToUnit(decimal value, UnitDefinition sourceUnit, UnitDefinition targetUnit) => ConvertFromBase(targetUnit, ConvertToBase(value, sourceUnit));

    private static Dictionary<string, UnitDefinition> CreateUnitAliasMap()
    {
        var aliasMap = new Dictionary<string, UnitDefinition>(StringComparer.Ordinal);

        foreach (var unit in s_allUnits)
        {
            foreach (var alias in unit.Aliases)
            {
                aliasMap[NormalizeUnitSearchText(alias)] = unit;
            }
        }

        return aliasMap;
    }

    private static string NormalizeUnitSearchText(string value) =>
        string.Concat(value.Trim().Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private enum UnitCategory
    {
        Length,
        Area,
        Volume,
        Mass,
        Temperature,
        Speed,
        Data
    }

    private sealed record UnitDefinition(string DisplayName, UnitCategory Category, bool IsMetric, decimal Factor, decimal Offset, string[] Aliases);

    private sealed record UnitConversionRequest(decimal Amount, UnitDefinition SourceUnit, string? TargetUnitQuery);

    private sealed record UnitConversionParseResult(UnitConversionRequest? Request, string? Message);

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("convert_unit", "Convert a value between units of length, area, volume, mass, temperature, speed, or data size.", DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
        {
            ["query"] = DengAiJsonSchemaProperty.String("Query in Korean command style, such as '100피트 미터', '1.5킬로미터', '25도 화씨'.")
        }))
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("convert_unit", StringComparison.Ordinal)) return "Unknown unit conversion tool.";

        var queryText = DengAiToolJson.ReadString(arguments, "query") ?? string.Empty;
        return await CreateUnitConversionMessageAsync(queryText);
    }

    #endregion
}
