using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

/// <summary>
/// Records a pending senderHash migration for a specific user after room migration.
/// When a user sends a message in the target room, their old senderHash data
/// is merged into the new senderHash and this mapping is deleted.
/// </summary>
public class RoomMigrationMapping
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("targetRoomId")]
    public string TargetRoomId { get; set; } = string.Empty;

    [BsonElement("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [BsonElement("oldSenderHash")]
    public string OldSenderHash { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }
}
