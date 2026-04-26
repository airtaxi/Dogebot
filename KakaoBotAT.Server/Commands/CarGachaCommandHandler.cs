using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using System.Text.Json;

namespace KakaoBotAT.Server.Commands;

/// <summary>
/// Handles the !차뽑기 command to randomly select a car.
/// 7% chance to get a sledge instead of a car.
/// </summary>
public class CarGachaCommandHandler : ICommandHandler
{
    private readonly ILogger<CarGachaCommandHandler> _logger;
    private readonly Random _random = new();
    private static List<CarData>? _carData;
    private static readonly object _lock = new();

    public CarGachaCommandHandler(ILogger<CarGachaCommandHandler> logger)
    {
        _logger = logger;
        LoadCarData();
    }

    public string Command => "!차뽑기";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            // 7% chance to get a sledge
            if (_random.Next(0, 100) < 7)
            {
                var message = "🛷 축하합니다!\n\n🎉 썰매를 획득했습니다! 🎉\n\n" +
                             "⛄ 겨울의 로망, 썰매!\n" +
                             "눈 오는 날 타기 딱 좋은 썰매입니다! ❄️";

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("[CAR_GACHA] {Sender} got a SLEDGE in room {RoomId}", 
                        data.SenderName, data.RoomId);

                return Task.FromResult(new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = message
                });
            }

            // Select random car
            if (_carData == null || _carData.Count == 0)
            {
                return Task.FromResult(new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 차량 데이터를 불러올 수 없습니다."
                });
            }

            var brand = _carData[_random.Next(_carData.Count)];
            var model = brand.Models[_random.Next(brand.Models.Count)];
            var trim = model.Trims[_random.Next(model.Trims.Count)];

            var message2 = $"🚗 차량 뽑기 결과\n\n" +
                          $"제조사: {brand.Brand}\n" +
                          $"모델: {model.Name}\n" +
                          $"트림: {trim}";

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[CAR_GACHA] {Sender} got {Brand} {Model} {Trim} in room {RoomId}", 
                    data.SenderName, brand.Brand, model.Name, trim, data.RoomId);

            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message2
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CAR_GACHA] Error processing car gacha command");
            return Task.FromResult(new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "차량 뽑기 중 오류가 발생했습니다."
            });
        }
    }

    private void LoadCarData()
    {
        if (_carData != null)
            return;

        lock (_lock)
        {
            if (_carData != null)
                return;

            try
            {
                var jsonPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Car.json");
                
                if (!File.Exists(jsonPath))
                {
                    _logger.LogError("[CAR_GACHA] Car.json not found at {Path}", jsonPath);
                    _carData = new List<CarData>();
                    return;
                }

                var jsonContent = File.ReadAllText(jsonPath);
                _carData = JsonSerializer.Deserialize<List<CarData>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<CarData>();

                _logger.LogInformation("[CAR_GACHA] Loaded {Count} car brands from Car.json", _carData.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CAR_GACHA] Error loading Car.json");
                _carData = new List<CarData>();
            }
        }
    }
}
