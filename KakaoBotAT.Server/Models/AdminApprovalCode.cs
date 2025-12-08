using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

public class AdminApprovalCode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [BsonElement("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("roomName")]
    public string RoomName { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    [BsonElement("expiresAt")]
    public long ExpiresAt { get; set; }
}
