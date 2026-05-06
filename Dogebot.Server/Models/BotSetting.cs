using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public enum MessageDeliveryMode
{
    Single,
    Multi
}

public class BotSetting
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("value")]
    public string Value { get; set; } = string.Empty;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; } = string.Empty;

    [BsonElement("updatedAt")]
    public long UpdatedAt { get; set; }
}
