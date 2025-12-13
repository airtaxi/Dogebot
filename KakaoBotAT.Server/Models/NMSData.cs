using System.Text.Json;

namespace KakaoBotAT.Server.Models;

public class NMSData
{
    public Dictionary<string, Biome>? Biomes { get; set; }
    public Dictionary<string, StarSystem>? StarSystems { get; set; }
}

public class Biome
{
    public List<string>? Prefixes { get; set; }
    public JsonElement Weather { get; set; }
    public List<string>? ExclusivePlants { get; set; }
    public List<string>? ExclusiveResources { get; set; }
}

public class BiomeWeatherObject
{
    public List<string>? Clear { get; set; }
    public List<string>? Normal { get; set; }
    public List<string>? Extreme { get; set; }
}

public class StarSystem
{
    public List<string>? ExclusiveResources { get; set; }
}
