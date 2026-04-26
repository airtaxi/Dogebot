using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

public class RoomMigrationCode
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("sourceRoomId")]
    public string SourceRoomId { get; set; } = string.Empty;

    [BsonElement("sourceRoomName")]
    public string SourceRoomName { get; set; } = string.Empty;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("createdByName")]
    public string CreatedByName { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    [BsonElement("expiresAt")]
    public long ExpiresAt { get; set; }
}
