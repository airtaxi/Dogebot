using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using System.Text.Json;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !행성뽑기 command to randomly generate a No Man's Sky planet.
/// </summary>
public class PlanetGachaCommandHandler : ICommandHandler
{
    private readonly ILogger<PlanetGachaCommandHandler> _logger;
    private readonly Random _random = new();
    private static NMSData? _nmsData;
    private static readonly object _lock = new();

    public PlanetGachaCommandHandler(ILogger<PlanetGachaCommandHandler> logger)
    {
        _logger = logger;
        LoadNMSData();
    }

    public string Command => "!행성뽑기";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            if (_nmsData == null)
            {
                return Task.FromResult(new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 행성 데이터를 불러올 수 없습니다."
                });
            }

            var message = GeneratePlanet();

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[PLANET_GACHA] {Sender} generated planet in room {RoomId}", 
                    data.SenderName, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PLANET_GACHA] Error processing planet gacha command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "행성 생성 중 오류가 발생했습니다."
            });
        }
    }

    private string GeneratePlanet()
    {
        if (_nmsData == null || _nmsData.Biomes == null)
            return "❌ 행성 데이터를 불러올 수 없습니다.";

        // Define special biome groups with weighted probabilities
        var specialBiomes = new[] { "WIRECELLS", "CONTOUR", "BONESPIRE", "IRRISHELLS", 
                                   "HYDROGARDEN", "MSTRUCT", "BEAMS", "HEXAGON", 
                                   "FRACTCUBE", "BUBBLE", "SHARDS", "GLITCH" };
        var colorBiomes = new[] { "RED", "GREEN", "BLUE" };
        
        // Calculate probabilities
        // Special biomes: 10% total (shared among all special biomes)
        // Color biomes: 3% each = 9% total
        // Normal biomes: remaining 81%
        
        var biomeKeys = _nmsData.Biomes.Keys.ToList();
        var normalBiomes = biomeKeys.Except(specialBiomes).Except(colorBiomes).ToList();
        
        string selectedBiomeKey;
        var roll = _random.Next(0, 100);
        
        if (roll < 3)
        {
            // 3% chance for RED
            selectedBiomeKey = "RED";
        }
        else if (roll < 6)
        {
            // 3% chance for GREEN
            selectedBiomeKey = "GREEN";
        }
        else if (roll < 9)
        {
            // 3% chance for BLUE
            selectedBiomeKey = "BLUE";
        }
        else if (roll < 19)
        {
            // 10% chance for special biomes (randomly select one)
            var availableSpecialBiomes = specialBiomes.Where(b => biomeKeys.Contains(b)).ToList();
            selectedBiomeKey = availableSpecialBiomes.Count > 0 
                ? availableSpecialBiomes[_random.Next(availableSpecialBiomes.Count)]
                : normalBiomes[_random.Next(normalBiomes.Count)];
        }
        else
        {
            // 81% chance for normal biomes
            selectedBiomeKey = normalBiomes[_random.Next(normalBiomes.Count)];
        }
        
        var selectedBiome = _nmsData.Biomes[selectedBiomeKey];

        // Determine star system based on biome
        string starSystemKey;
        if (selectedBiomeKey == "RED")
            starSystemKey = "RED";
        else if (selectedBiomeKey == "GREEN")
            starSystemKey = "GREEN";
        else if (selectedBiomeKey == "BLUE")
            starSystemKey = "BLUE";
        else
        {
            // 87.5% chance for YELLOW, 12.5% for others
            var roll2 = _random.Next(0, 100);
            if (roll2 < 87.5)
                starSystemKey = "YELLOW";
            else
            {
                var otherSystems = new[] { "RED", "GREEN", "BLUE" };
                starSystemKey = otherSystems[_random.Next(otherSystems.Length)];
            }
        }

        var starSystem = _nmsData.StarSystems?[starSystemKey];

        // Select prefix
        var prefix = selectedBiome.Prefixes?[_random.Next(selectedBiome.Prefixes.Count)] ?? "알 수 없는";

        // Select weather
        string weather;
        string weatherType;
        
        if (selectedBiome.Weather is JsonElement weatherElement)
        {
            // For biomes with weather as array (WIRECELLS, CONTOUR, etc.)
            if (weatherElement.ValueKind == JsonValueKind.Array)
            {
                var weatherArray = JsonSerializer.Deserialize<List<string>>(weatherElement.GetRawText()) ?? new List<string>();
                weather = weatherArray.Count > 0 ? weatherArray[_random.Next(weatherArray.Count)] : "알 수 없음";
                weatherType = "normal";
            }
            // For biomes with weather as object with clear/normal/extreme
            else if (weatherElement.ValueKind == JsonValueKind.Object)
            {
                var weatherObj = JsonSerializer.Deserialize<BiomeWeatherObject>(weatherElement.GetRawText());
                
                // Select weather type randomly (equal probability)
                var weatherTypes = new List<string>();
                if (weatherObj?.Clear?.Count > 0) weatherTypes.Add("clear");
                if (weatherObj?.Normal?.Count > 0) weatherTypes.Add("normal");
                if (weatherObj?.Extreme?.Count > 0) weatherTypes.Add("extreme");

                weatherType = weatherTypes.Count > 0 ? weatherTypes[_random.Next(weatherTypes.Count)] : "normal";
                
                weather = weatherType switch
                {
                    "clear" => weatherObj?.Clear?[_random.Next(weatherObj.Clear.Count)] ?? "알 수 없는 날씨",
                    "extreme" => weatherObj?.Extreme?[_random.Next(weatherObj.Extreme.Count)] ?? "알 수 없는 날씨",
                    _ => weatherObj?.Normal?[_random.Next(weatherObj.Normal.Count)] ?? "알 수 없는 날씨"
                };
            }
            else
            {
                weather = "알 수 없음";
                weatherType = "normal";
            }
        }
        else
        {
            weather = "알 수 없음";
            weatherType = "normal";
        }

        // Build resources list
        var resources = new List<string>();

        // 17% chance for ancient bones or salvageable scrap
        if (_random.Next(0, 100) < 17)
        {
            resources.Add(_random.Next(0, 2) == 0 ? "고대 뼈" : "노획 가능한 고물");
        }

        // Add exclusive plants
        if (selectedBiome.ExclusivePlants?.Count > 0)
        {
            foreach (var plant in selectedBiome.ExclusivePlants)
            {
                if (plant != "없음")
                    resources.Add(plant);
            }
        }

        // Add biome exclusive resources
        if (selectedBiome.ExclusiveResources?.Count > 0)
        {
            // Resources with 20% appearance chance (gases)
            var gasResources = new[] { "질소", "설퍼린", "라돈" };
            
            foreach (var resource in selectedBiome.ExclusiveResources)
            {
                // 20% chance for gas resources only
                if (gasResources.Contains(resource))
                {
                    if (_random.Next(0, 100) < 20)
                    {
                        resources.Add(resource);
                    }
                }
                else
                {
                    // 100% chance for other resources
                    resources.Add(resource);
                }
            }
        }

        // Add star system exclusive resource
        if (starSystem?.ExclusiveResources?.Count > 0)
        {
            var starResource = starSystem.ExclusiveResources[0];
            // Add "활성" prefix for extreme weather
            if (weatherType == "extreme")
                starResource = $"활성 {starResource}";
            resources.Add(starResource);
        }

        var hasVileBrood = _random.Next(0, 100) < 10;

        // 10% chance for dissonance detected
        var hasDissonance = _random.Next(0, 100) < 10;

        // 22% chance for high sentinel activity
        var hasHighSentinel = _random.Next(0, 100) < 22;

        // Build message
        var message = $"방금 발견한 노 맨즈 스카이 행성이다!\n\n" +
                     $"{prefix} 행성\n" +
                     $"☁️ 날씨: {weather}\n\n";

        // Add optional flags
        if (hasVileBrood)
            message += "- 끔찍한 무리 감지됨\n";

        // Add resources
        foreach (var resource in resources)
        {
            message += $"- {resource}\n";
        }

        // Add optional flags
        if (hasDissonance)
            message += "- 부조화 감지됨\n";
        
        if (hasHighSentinel)
            message += "- 센티널의 활동량 높음\n";

        return message.TrimEnd();
    }

    private void LoadNMSData()
    {
        if (_nmsData != null)
            return;

        lock (_lock)
        {
            if (_nmsData != null)
                return;

            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "NMS.json");
                
                if (!File.Exists(jsonPath))
                {
                    _logger.LogError("[PLANET_GACHA] NMS.json not found at {Path}", jsonPath);
                    return;
                }

                var jsonContent = File.ReadAllText(jsonPath);
                _nmsData = JsonSerializer.Deserialize<NMSData>(jsonContent);

                _logger.LogInformation("[PLANET_GACHA] Loaded NMS data from NMS.json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PLANET_GACHA] Error loading NMS.json");
            }
        }
    }
}
