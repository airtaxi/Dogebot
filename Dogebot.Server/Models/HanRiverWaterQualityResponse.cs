using System.Text.Json.Serialization;

namespace Dogebot.Server.Models;

public class WposInformationTimeResponse
{
    [JsonPropertyName("WPOSInformationTime")]
    public WposInformationTimeData WposInformationTime { get; set; } = new();
}

public class WposInformationTimeData
{
    [JsonPropertyName("list_total_count")]
    public int ListTotalCount { get; set; }

    [JsonPropertyName("RESULT")]
    public WposResultData Result { get; set; } = new();

    [JsonPropertyName("row")]
    public List<WposWaterQualityRow> Row { get; set; } = [];
}

public class WposResultData
{
    [JsonPropertyName("CODE")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("MESSAGE")]
    public string Message { get; set; } = string.Empty;
}

public class WposWaterQualityRow
{
    [JsonPropertyName("YMD")]
    public string Ymd { get; set; } = string.Empty;

    [JsonPropertyName("HR")]
    public string Hr { get; set; } = string.Empty;

    [JsonPropertyName("MSRSTN_NM")]
    public string StationName { get; set; } = string.Empty;

    [JsonPropertyName("WATT")]
    public string WaterTemperature { get; set; } = string.Empty;

    [JsonPropertyName("TOT_PH")]
    public string Ph { get; set; } = string.Empty;

    [JsonPropertyName("TOT_DO")]
    public string DissolvedOxygen { get; set; } = string.Empty;

    [JsonPropertyName("TOT_N")]
    public string TotalNitrogen { get; set; } = string.Empty;

    [JsonPropertyName("TOT_TP")]
    public string TotalPhosphorus { get; set; } = string.Empty;

    [JsonPropertyName("TOT_OC")]
    public string TotalOrganicCarbon { get; set; } = string.Empty;

    [JsonPropertyName("PHNL")]
    public string Phenol { get; set; } = string.Empty;

    [JsonPropertyName("CN")]
    public string Cyanide { get; set; } = string.Empty;
}