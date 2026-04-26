using System.Text.Json;
using System.Text.Json.Serialization;

namespace KakaoBotAT.Server.Models;

public class NMSData
{
    [JsonPropertyName("biomes")]
    public Dictionary<string, Biome>? Biomes { get; set; }
    
    [JsonPropertyName("starSystems")]
    public Dictionary<string, StarSystem>? StarSystems { get; set; }
}

public class Biome
{
    [JsonPropertyName("prefixes")]
    public List<string>? Prefixes { get; set; }
    
    [JsonPropertyName("weather")]
    public JsonElement Weather { get; set; }
    
    [JsonPropertyName("exclusivePlants")]
    public List<string>? ExclusivePlants { get; set; }
    
    [JsonPropertyName("exclusiveResources")]
    public List<string>? ExclusiveResources { get; set; }
}

public class BiomeWeatherObject
{
    [JsonPropertyName("clear")]
    public List<string>? Clear { get; set; }
    
    [JsonPropertyName("normal")]
    public List<string>? Normal { get; set; }
    
    [JsonPropertyName("extreme")]
    public List<string>? Extreme { get; set; }
}

public class StarSystem
{
    [JsonPropertyName("exclusiveResources")]
    public List<string>? ExclusiveResources { get; set; }
}
