using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public class DailyFortuneRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [BsonElement("date")]
    public string Date { get; set; } = string.Empty;
}

