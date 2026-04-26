using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

public class RoomRequestLimitApprovalCode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("roomName")]
    public string RoomName { get; set; } = string.Empty;

    [BsonElement("dailyLimit")]
    public int DailyLimit { get; set; }

    [BsonElement("requestedBy")]
    public string RequestedBy { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    [BsonElement("expiresAt")]
    public long ExpiresAt { get; set; }
}
