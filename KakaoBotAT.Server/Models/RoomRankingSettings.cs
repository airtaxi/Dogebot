using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

public class RoomRankingSettings
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("roomName")]
    public string RoomName { get; set; } = string.Empty;

    [BsonElement("isMessageContentEnabled")]
    public bool IsMessageContentEnabled { get; set; } = true;

    [BsonElement("setBy")]
    public string SetBy { get; set; } = string.Empty;

    [BsonElement("setAt")]
    public long SetAt { get; set; }
}
