using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

public class UserDailyRequest
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [BsonElement("date")]
    public string Date { get; set; } = string.Empty; // Format: yyyy-MM-dd

    [BsonElement("requestCount")]
    public int RequestCount { get; set; }

    [BsonElement("lastRequestTime")]
    public long LastRequestTime { get; set; }
}
