using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

public class RoomRequestLimit
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("roomName")]
    public string RoomName { get; set; } = string.Empty;

    [BsonElement("dailyLimit")]
    public int DailyLimit { get; set; }

    [BsonElement("setBy")]
    public string SetBy { get; set; } = string.Empty;

    [BsonElement("setAt")]
    public long SetAt { get; set; }
}
