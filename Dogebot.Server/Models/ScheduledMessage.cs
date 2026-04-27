using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public class ScheduledMessage
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Days of week (0=Sunday .. 6=Saturday) on which this message should be sent.
    /// All 7 days means "전체" (every day).
    /// </summary>
    [BsonElement("days")]
    public List<int> Days { get; set; } = [];

    /// <summary>
    /// Hours (0-23, KST) at which this message should be sent.
    /// </summary>
    [BsonElement("hours")]
    public List<int> Hours { get; set; } = [];

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("createdByName")]
    public string CreatedByName { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }
}

