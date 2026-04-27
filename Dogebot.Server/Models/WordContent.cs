using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public class WordContent
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("word")]
    public string Word { get; set; } = string.Empty;

    [BsonElement("count")]
    public long Count { get; set; }

    [BsonElement("lastTime")]
    public long LastTime { get; set; }
}

