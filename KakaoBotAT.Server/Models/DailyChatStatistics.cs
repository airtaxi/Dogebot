using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

/// <summary>
/// Tracks message counts per day of week for each user in a room.
/// DayOfWeek is stored in KST (0=Sunday to 6=Saturday).
/// </summary>
public class DailyChatStatistics
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [BsonElement("dayOfWeek")]
    public int DayOfWeek { get; set; }

    [BsonElement("messageCount")]
    public long MessageCount { get; set; }
}
