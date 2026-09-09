using System.Text;
using System.Text.Json;
using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

/// <summary>
/// Provides the latest Han River water quality measurements from the Seoul Open Data API (WPOSInformationTime).
/// </summary>
public class HanRiverWaterQualityService(IHttpClientFactory httpClientFactory, ILogger<HanRiverWaterQualityService> logger) : IHanRiverWaterQualityService
{
    private const string ApiKeyEnvironmentVariableName = "DOGEBOT_SEOUL_API_KEY";
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private readonly string? _apiKey = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariableName);

    public async Task<IReadOnlyList<WposWaterQualityRow>?> GetLatestWaterQualityAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            logger.LogError("[HAN_RIVER] API key is not configured. Required environment variable: {EnvironmentVariableName}", ApiKeyEnvironmentVariableName);
            return null;
        }

        try
        {
            var url = $"http://openapi.seoul.go.kr:8088/{_apiKey}/json/WPOSInformationTime/1/5/";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("[HAN_RIVER] Seoul water quality API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var waterQualityData = JsonSerializer.Deserialize<WposInformationTimeResponse>(content);

            if (waterQualityData?.WposInformationTime is not { } informationTime || informationTime.Row.Count == 0)
            {
                logger.LogWarning("[HAN_RIVER] No water quality data received from API");
                return null;
            }

            if (!informationTime.Result.Code.Equals("INFO-000", StringComparison.Ordinal))
            {
                logger.LogWarning("[HAN_RIVER] API returned error result: {Code} {Message}", informationTime.Result.Code, informationTime.Result.Message);
                return null;
            }

            return DeduplicateByStation(informationTime.Row);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[HAN_RIVER] Error fetching water quality data");
            return null;
        }
    }

    private static IReadOnlyList<WposWaterQualityRow> DeduplicateByStation(IReadOnlyList<WposWaterQualityRow> rows)
    {
        var waterQualityRows = new List<WposWaterQualityRow>();
        var seenStations = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.StationName)) continue;
            if (!seenStations.Add(row.StationName)) continue;
            waterQualityRows.Add(row);
        }

        return waterQualityRows;
    }

    public static string FormatWaterQualityMessage(IReadOnlyList<WposWaterQualityRow> waterQualityRows)
    {
        var latestRow = waterQualityRows[0];
        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine("🌊 한강 수질 정보");
        stringBuilder.AppendLine($"📅 {FormatMeasurementDate(latestRow.Ymd)} ({latestRow.Hr} 기준)");
        stringBuilder.AppendLine();

        foreach (var row in waterQualityRows)
        {
            stringBuilder.AppendLine($"🏞️ {row.StationName}");
            stringBuilder.AppendLine($"수온 {row.WaterTemperature}°C | pH {row.Ph}");
            stringBuilder.AppendLine($"용존산소 {row.DissolvedOxygen}mg/L | 총질소 {row.TotalNitrogen}mg/L | 총인 {row.TotalPhosphorus}mg/L");
            stringBuilder.AppendLine($"총유기탄소 {row.TotalOrganicCarbon}mg/L | 페놀 {row.Phenol}mg/L | 시안 {row.Cyanide}mg/L");
            stringBuilder.AppendLine();
        }

        return stringBuilder.ToString().ReplaceLineEndings("\n").TrimEnd('\n');
    }

    private static string FormatMeasurementDate(string ymd)
    {
        if (ymd.Length != 8) return ymd;
        return $"{ymd[..4]}.{ymd[4..6]}.{ymd[6..8]}";
    }

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_han_river_water_quality", "Get the latest Han River water quality measurements by station (water temperature, pH, dissolved oxygen, total nitrogen, total phosphorus, total organic carbon, phenol, cyanide).", DengAiJsonSchema.Object())
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "get_han_river_water_quality" => await CreateWaterQualityToolResultAsync(cancellationToken),
            _ => "Unknown water quality tool."
        };
    }

    private async Task<string> CreateWaterQualityToolResultAsync(CancellationToken cancellationToken)
    {
        var waterQualityRows = await GetLatestWaterQualityAsync(cancellationToken);
        if (waterQualityRows is null || waterQualityRows.Count == 0) return "한강 수질 정보를 가져올 수 없습니다.";
        return FormatWaterQualityMessage(waterQualityRows);
    }

    #endregion
}