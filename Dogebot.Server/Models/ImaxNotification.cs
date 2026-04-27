using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public class ImaxNotification
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// Screening date in yyyyMMdd format (e.g., "20260330").
    /// </summary>
    [BsonElement("screeningDate")]
    public string ScreeningDate { get; set; } = string.Empty;

    /// <summary>
    /// Optional keyword for KakaoTalk keyword notifications.
    /// </summary>
    [BsonElement("keyword")]
    public string? Keyword { get; set; }

    /// <summary>
    /// Movie name displayed to users (e.g., "프로젝트 헤일메리").
    /// </summary>
    [BsonElement("movieName")]
    public string MovieName { get; set; } = string.Empty;

    /// <summary>
    /// CGV movie number used for API calls (e.g., "30000994").
    /// </summary>
    [BsonElement("movieNumber")]
    public string MovieNumber { get; set; } = string.Empty;

    /// <summary>
    /// CGV site number (e.g., "0013" for 용산아이파크몰).
    /// </summary>
    [BsonElement("siteNumber")]
    public string SiteNumber { get; set; } = string.Empty;

    /// <summary>
    /// CGV site name displayed to users (e.g., "용산아이파크몰").
    /// </summary>
    [BsonElement("siteName")]
    public string SiteName { get; set; } = string.Empty;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("createdByName")]
    public string CreatedByName { get; set; } = string.Empty;

    [BsonElement("roomName")]
    public string RoomName { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    /// <summary>
    /// Formatted message to send when IMAX is detected. Null means IMAX has not been detected yet.
    /// Set by the background check service, consumed and deleted by the delivery check.
    /// </summary>
    [BsonElement("pendingMessage")]
    public string? PendingMessage { get; set; }
}

