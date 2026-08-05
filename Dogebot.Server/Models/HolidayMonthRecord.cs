using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Dogebot.Server.Models;

public class HolidayMonthRecord
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("yearMonth")]
    public string YearMonth { get; set; } = string.Empty;

    [BsonElement("holidays")]
    public List<HolidayEntry> Holidays { get; set; } = [];

    public class HolidayEntry
    {
        [BsonElement("date")]
        public string Date { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;
    }
}
