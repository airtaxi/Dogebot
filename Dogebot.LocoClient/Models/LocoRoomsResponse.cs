using System.Text.Json.Serialization;

namespace Dogebot.LocoClient.Models;

/// <summary>
/// Response body of GET /api/rooms.
/// </summary>
public class LocoRoomsResponse
{
    [JsonPropertyName("rooms")]
    public List<LocoRoom> Rooms { get; set; } = [];
}
