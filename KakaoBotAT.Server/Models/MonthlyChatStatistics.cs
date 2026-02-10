using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

/// <summary>
/// Tracks message counts per month (1-12) for each user in a room.
/// Month is stored in KST.
/// </summary>
public class MonthlyChatStatistics
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [BsonElement("month")]
    public int Month { get; set; }

    [BsonElement("messageCount")]
    public long MessageCount { get; set; }
}
