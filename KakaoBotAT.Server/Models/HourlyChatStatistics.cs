using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

/// <summary>
/// Tracks message counts per time slot for each room.
/// DateTime is truncated to the minute for future minute-level analysis.
/// </summary>
public class HourlyChatStatistics
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [BsonElement("dateTime")]
    public DateTime DateTime { get; set; }

    [BsonElement("messageCount")]
    public long MessageCount { get; set; }
}
