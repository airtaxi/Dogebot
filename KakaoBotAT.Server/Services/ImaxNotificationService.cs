using System.Globalization;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class ImaxNotificationService : IImaxNotificationService
{
    private readonly IMongoCollection<ImaxNotification> _imaxNotifications;
    private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);

    public ImaxNotificationService(IMongoDbService mongoDbService)
    {
        _imaxNotifications = mongoDbService.Database.GetCollection<ImaxNotification>("imaxNotifications");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var roomIdIndex = new CreateIndexModel<ImaxNotification>(
            Builders<ImaxNotification>.IndexKeys.Ascending(x => x.RoomId),
            new CreateIndexOptions { Unique = true });
        _imaxNotifications.Indexes.CreateOne(roomIdIndex);

        var dateIndex = new CreateIndexModel<ImaxNotification>(
            Builders<ImaxNotification>.IndexKeys.Ascending(x => x.ScreeningDate));
        _imaxNotifications.Indexes.CreateOne(dateIndex);
    }

    public async Task<(bool Success, string Message)> RegisterAsync(
        string roomId, string screeningDate, string? keyword,
        string senderHash, string senderName, string roomName)
    {
        var existing = await GetNotificationAsync(roomId);
        if (existing is not null)
        {
            var existingDateDisplay = FormatScreeningDate(existing.ScreeningDate);
            return (false, $"❌ 이 방에 이미 알림이 등록되어 있습니다.\n\n" +
                          $"📅 기존 알림: {existingDateDisplay}\n\n" +
                          $"!용아맥해제 후 다시 등록해주세요.");
        }

        var notification = new ImaxNotification
        {
            RoomId = roomId,
            ScreeningDate = screeningDate,
            Keyword = keyword,
            CreatedBy = senderHash,
            CreatedByName = senderName,
            RoomName = roomName,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        try
        {
            await _imaxNotifications.InsertOneAsync(notification);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return (false, "❌ 이 방에 이미 알림이 등록되어 있습니다.\n!용아맥해제 후 다시 등록해주세요.");
        }

        var dateDisplay = FormatScreeningDate(screeningDate);
        var keywordDisplay = string.IsNullOrEmpty(keyword) ? "" : $"\n🔑 키워드: {keyword}";
        return (true, $"✅ 용아맥 알림이 등록되었습니다!\n\n" +
                      $"📅 날짜: {dateDisplay}{keywordDisplay}\n" +
                      $"⏰ 5분 간격으로 IMAX 상영 여부를 확인합니다.\n\n" +
                      $"IMAX 감지 시 자동으로 알림이 전송되고 해제됩니다.\n" +
                      $"⚠️ 알림은 채팅이 올 때 답장으로 전송되므로,\n" +
                      $"등록 후 최소 1건 이상의 채팅이 필요합니다.");
    }

    public async Task<ImaxNotification?> GetNotificationAsync(string roomId)
    {
        var filter = Builders<ImaxNotification>.Filter.Eq(x => x.RoomId, roomId);
        return await _imaxNotifications.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<List<ImaxNotification>> GetAllActiveNotificationsAsync()
    {
        return await _imaxNotifications.Find(FilterDefinition<ImaxNotification>.Empty).ToListAsync();
    }

    public async Task<bool> RemoveNotificationAsync(string roomId)
    {
        var filter = Builders<ImaxNotification>.Filter.Eq(x => x.RoomId, roomId);
        var result = await _imaxNotifications.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task SetPendingMessageAsync(string notificationId, string message)
    {
        var filter = Builders<ImaxNotification>.Filter.Eq(x => x.Id, notificationId);
        var update = Builders<ImaxNotification>.Update.Set(x => x.PendingMessage, message);
        await _imaxNotifications.UpdateOneAsync(filter, update);
    }

    public async Task<ServerResponse?> CheckAndDeliverAsync(KakaoMessageData data)
    {
        var filter = Builders<ImaxNotification>.Filter.And(
            Builders<ImaxNotification>.Filter.Eq(x => x.RoomId, data.RoomId),
            Builders<ImaxNotification>.Filter.Ne(x => x.PendingMessage, null));

        // Atomically find and delete: prevents duplicate delivery
        var notification = await _imaxNotifications.FindOneAndDeleteAsync(filter);

        if (notification is null)
            return null;

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = notification.PendingMessage!
        };
    }

    public async Task<int> CleanupExpiredNotificationsAsync()
    {
        var todayStr = DateTimeOffset.UtcNow.ToOffset(KstOffset).ToString("yyyyMMdd");
        var filter = Builders<ImaxNotification>.Filter.Lt(x => x.ScreeningDate, todayStr);
        var result = await _imaxNotifications.DeleteManyAsync(filter);
        return (int)result.DeletedCount;
    }

    public static string FormatScreeningDate(string yyyyMMdd)
    {
        if (DateTime.TryParseExact(yyyyMMdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return $"{date.Year}년 {date.Month}월 {date.Day}일";
        return yyyyMMdd;
    }
}
