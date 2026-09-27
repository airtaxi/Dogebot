using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

/// <summary>
/// Stores the canonical sender identity for a room and sender name pair.
/// A mobile notification hash is registered as a provisional canonical and is replaced
/// by the LOCO account id once the LOCO bridge observes the same sender in the room.
/// Known identity values are recorded together so messages keep resolving after a nickname change.
/// </summary>
public class IdentityCanonical
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [BsonElement("canonicalValue")]
    public string CanonicalValue { get; set; } = string.Empty;

    [BsonElement("knownValues")]
    public List<string> KnownValues { get; set; } = [];

    [BsonElement("isLoco")]
    public bool IsLoco { get; set; }

    [BsonElement("isAmbiguous")]
    public bool IsAmbiguous { get; set; }

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public long UpdatedAt { get; set; }
}
