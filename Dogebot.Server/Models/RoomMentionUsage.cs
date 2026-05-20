using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public class RoomMentionUsage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("roomName")]
    public string RoomName { get; set; } = string.Empty;

    [BsonElement("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [BsonElement("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [BsonElement("lastUsedAt")]
    public long LastUsedAt { get; set; }

    [BsonElement("nextAvailableAt")]
    public long NextAvailableAt { get; set; }
}
