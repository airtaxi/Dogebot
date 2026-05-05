using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public class BaseballGameSubscriptionMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("subscriptionId")]
    public string SubscriptionId { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("gameDate")]
    public string GameDate { get; set; } = string.Empty;

    [BsonElement("gameId")]
    public long GameId { get; set; }

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }
}
