using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

public class MessageContent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("count")]
    public long Count { get; set; }

    [BsonElement("lastTime")]
    public long LastTime { get; set; }
}
