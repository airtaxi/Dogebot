using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KakaoBotAT.Server.Models;

public class UserWeatherPreference
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("senderHash")]
    public string SenderHash { get; set; } = string.Empty;

    [BsonElement("cityName")]
    public string CityName { get; set; } = string.Empty;

    [BsonElement("lastUpdated")]
    public long LastUpdated { get; set; }
}
