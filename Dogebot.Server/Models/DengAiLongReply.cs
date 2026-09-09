using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public class DengAiLongReply
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("urlHash")]
    public string UrlHash { get; set; } = string.Empty;

    [BsonElement("content")]
    public string Content { get; set; } = string.Empty;

    [BsonElement("expireAt")]
    public DateTime ExpireAt { get; set; }
}