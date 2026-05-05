using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public enum BaseballGameSubscriptionMode
{
    OneGame,
    Regular
}

public enum BaseballGameSubscriptionStatus
{
    Active,
    Completed,
    Canceled
}

public class BaseballGameSubscription
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("roomId")]
    public string RoomId { get; set; } = string.Empty;

    [BsonElement("roomName")]
    public string RoomName { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    [BsonElement("subscriptionMode")]
    public BaseballGameSubscriptionMode SubscriptionMode { get; set; } = BaseballGameSubscriptionMode.OneGame;

    [BsonElement("gameDate")]
    public string GameDate { get; set; } = string.Empty;

    [BsonElement("gameId")]
    public long GameId { get; set; }

    [BsonElement("homeTeamProviderId")]
    public string HomeTeamProviderId { get; set; } = string.Empty;

    [BsonElement("homeTeamName")]
    public string HomeTeamName { get; set; } = string.Empty;

    [BsonElement("homeTeamShortName")]
    public string HomeTeamShortName { get; set; } = string.Empty;

    [BsonElement("awayTeamProviderId")]
    public string AwayTeamProviderId { get; set; } = string.Empty;

    [BsonElement("awayTeamName")]
    public string AwayTeamName { get; set; } = string.Empty;

    [BsonElement("awayTeamShortName")]
    public string AwayTeamShortName { get; set; } = string.Empty;

    [BsonElement("subscribedTeamName")]
    public string SubscribedTeamName { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.String)]
    [BsonElement("status")]
    public BaseballGameSubscriptionStatus Status { get; set; } = BaseballGameSubscriptionStatus.Active;

    [BsonElement("lastDeliveredLiveEventKey")]
    public string LastDeliveredLiveEventKey { get; set; } = string.Empty;

    [BsonElement("lastDeliveredLiveEventIndex")]
    public int LastDeliveredLiveEventIndex { get; set; } = -1;

    [BsonElement("lastHomeScore")]
    public int? LastHomeScore { get; set; }

    [BsonElement("lastAwayScore")]
    public int? LastAwayScore { get; set; }

    [BsonElement("lineupNotified")]
    public bool LineupNotified { get; set; }

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;

    [BsonElement("createdByName")]
    public string CreatedByName { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public long CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public long UpdatedAt { get; set; }

    [BsonElement("completedAt")]
    public long? CompletedAt { get; set; }
}
