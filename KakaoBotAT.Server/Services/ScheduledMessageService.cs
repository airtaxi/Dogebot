using System.Collections.Concurrent;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class ScheduledMessageService : IScheduledMessageService
{
    private readonly IMongoCollection<ScheduledMessage> _scheduledMessages;

    private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);
    private const int MaxScheduledMessagesPerRoom = 24;
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// In-memory multi-stage setup sessions keyed by (RoomId, SenderHash).
    /// </summary>
    private readonly ConcurrentDictionary<(string RoomId, string SenderHash), SetupSession> _sessions = new();

    /// <summary>
    /// Tracks which (roomId, kstDate, hour) combinations have already been sent today.
    /// Key format: "{roomId}:{yyyy-MM-dd}:{hour}"
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _sentTracking = new();

    public ScheduledMessageService(IMongoDbService mongoDbService)
    {
        _scheduledMessages = mongoDbService.Database.GetCollection<ScheduledMessage>("scheduledMessages");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<ScheduledMessage>.IndexKeys.Ascending(x => x.RoomId);
        var indexModel = new CreateIndexModel<ScheduledMessage>(indexKeys);
        _scheduledMessages.Indexes.CreateOne(indexModel);
    }

    public void StartSession(string roomId, string senderHash, string senderName, string roomName)
    {
        var session = new SetupSession
        {
            RoomId = roomId,
            SenderHash = senderHash,
            SenderName = senderName,
            RoomName = roomName,
            Stage = SetupStage.AwaitingMessage,
            LastActivityAt = DateTimeOffset.UtcNow
        };

        _sessions[(roomId, senderHash)] = session;
    }

    public async Task<ServerResponse?> HandleSessionInputAsync(KakaoMessageData data)
    {
        var key = (data.RoomId, data.SenderHash);
        if (!_sessions.TryGetValue(key, out var session))
            return null;

        // Check session timeout
        if (DateTimeOffset.UtcNow - session.LastActivityAt > SessionTimeout)
        {
            _sessions.TryRemove(key, out _);
            return null;
        }

        var trimmed = data.Content.Trim();

        // Allow !취소 to cancel the session
        if (trimmed.Equals("!취소", StringComparison.OrdinalIgnoreCase))
        {
            _sessions.TryRemove(key, out _);
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 반복 메시지 설정이 취소되었습니다."
            };
        }

        // Let other ! commands pass through to normal command routing
        if (trimmed.StartsWith('!'))
            return null;

        session.LastActivityAt = DateTimeOffset.UtcNow;

        return session.Stage switch
        {
            SetupStage.AwaitingMessage => HandleAwaitingMessage(session, data),
            SetupStage.AwaitingDays => HandleAwaitingDays(session, data),
            SetupStage.AwaitingHours => await HandleAwaitingHoursAsync(session, data),
            _ => null
        };
    }

    private ServerResponse HandleAwaitingMessage(SetupSession session, KakaoMessageData data)
    {
        session.Message = data.Content;
        session.Stage = SetupStage.AwaitingDays;

        var preview = session.Message.Length > 50
            ? session.Message[..47] + "..."
            : session.Message;

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = $"✅ 메시지 저장됨\n\n" +
                     $"📝 \"{preview}\"\n\n" +
                     $"📅 보낼 요일을 입력해주세요.\n" +
                     $"공백으로 구분 (월 화 수 목 금 토 일)\n" +
                     $"매일 보내려면: 전체\n\n" +
                     $"예: 월 수 금\n\n" +
                     $"❌ 취소: !취소"
        };
    }

    private ServerResponse HandleAwaitingDays(SetupSession session, KakaoMessageData data)
    {
        var input = data.Content.Trim();
        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // "전체" handling
        if (parts.Length == 1 && parts[0].Equals("전체", StringComparison.OrdinalIgnoreCase))
        {
            session.Days = AllDays;
            session.Stage = SetupStage.AwaitingHours;

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 요일 저장됨: 전체\n\n" +
                         $"⏰ 보낼 시간을 입력해주세요. (0~23시)\n" +
                         $"여러 시간은 공백으로 구분\n" +
                         $"예: 9 15 21\n\n" +
                         $"❌ 취소: !취소"
            };
        }

        // Validate: "전체" cannot be mixed with individual days
        if (parts.Any(p => p.Equals("전체", StringComparison.OrdinalIgnoreCase)) && parts.Length > 1)
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ '전체'는 다른 요일과 함께 사용할 수 없습니다.\n" +
                         "'전체' 또는 개별 요일만 입력해주세요.\n\n" +
                         "예: 월 수 금\n" +
                         "예: 전체\n\n" +
                         "❌ 취소: !취소"
            };
        }

        var days = new List<int>();

        foreach (var part in parts)
        {
            if (!DayNameMap.TryGetValue(part, out var dayOfWeek))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ \"{part}\"은(는) 유효하지 않은 요일입니다.\n" +
                             $"월 화 수 목 금 토 일 중에서 입력해주세요.\n\n" +
                             $"예: 월 수 금\n" +
                             $"예: 전체\n\n" +
                             $"❌ 취소: !취소"
                };
            }

            var dayValue = (int)dayOfWeek;
            if (days.Contains(dayValue))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ \"{part}\"이(가) 중복되었습니다.\n" +
                             $"각 요일은 한 번만 입력해주세요.\n\n" +
                             $"❌ 취소: !취소"
                };
            }

            days.Add(dayValue);
        }

        if (days.Count == 0)
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 최소 1개의 요일을 입력해주세요.\n" +
                         "예: 월 수 금\n" +
                         "예: 전체\n\n" +
                         "❌ 취소: !취소"
            };
        }

        days.Sort();
        session.Days = days;
        session.Stage = SetupStage.AwaitingHours;

        var daysDisplay = FormatDays(days);

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = $"✅ 요일 저장됨: {daysDisplay}\n\n" +
                     $"⏰ 보낼 시간을 입력해주세요. (0~23시)\n" +
                     $"여러 시간은 공백으로 구분\n" +
                     $"예: 9 15 21\n\n" +
                     $"❌ 취소: !취소"
        };
    }

    private async Task<ServerResponse> HandleAwaitingHoursAsync(SetupSession session, KakaoMessageData data)
    {
        var key = (session.RoomId, session.SenderHash);
        var parts = data.Content.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hours = new List<int>();

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var hour) || hour < 0 || hour > 23)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ \"{part}\"은(는) 유효하지 않은 시간입니다.\n" +
                             $"0~23 사이의 숫자를 공백으로 구분하여 입력해주세요.\n" +
                             $"예: 9 15 21\n\n" +
                             $"❌ 취소: !취소"
                };
            }

            if (!hours.Contains(hour))
                hours.Add(hour);
        }

        if (hours.Count == 0)
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 최소 1개의 시간을 입력해주세요.\n" +
                         "예: 9 15 21\n\n" +
                         "❌ 취소: !취소"
            };
        }

        // Check room limit
        var existingCount = await _scheduledMessages.CountDocumentsAsync(
            Builders<ScheduledMessage>.Filter.Eq(x => x.RoomId, session.RoomId));

        if (existingCount >= MaxScheduledMessagesPerRoom)
        {
            _sessions.TryRemove(key, out _);
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"❌ 이 방의 반복 메시지가 최대 {MaxScheduledMessagesPerRoom}개입니다.\n" +
                         $"기존 메시지를 삭제한 후 다시 시도해주세요.\n" +
                         $"(!반복목록으로 확인, !반복해제로 삭제)"
            };
        }

        hours.Sort();

        var scheduledMessage = new ScheduledMessage
        {
            RoomId = session.RoomId,
            Message = session.Message!,
            Days = session.Days!,
            Hours = hours,
            CreatedBy = session.SenderHash,
            CreatedByName = session.SenderName,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _scheduledMessages.InsertOneAsync(scheduledMessage);
        _sessions.TryRemove(key, out _);

        var daysDisplay = FormatDays(session.Days!);
        var hoursDisplay = string.Join(", ", hours.Select(h => $"{h}시"));
        var messagePreview = session.Message!.Length > 50
            ? session.Message[..47] + "..."
            : session.Message;

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = $"✅ 반복 메시지 설정 완료!\n\n" +
                     $"📝 메시지: \"{messagePreview}\"\n" +
                     $"📅 요일: {daysDisplay}\n" +
                     $"⏰ 시간: {hoursDisplay}\n\n" +
                     $"설정된 요일/시간에 채팅이 오면 자동으로 답장합니다."
        };
    }

    public async Task<ServerResponse?> CheckAndSendScheduledMessageAsync(KakaoMessageData data)
    {
        var now = DateTimeOffset.UtcNow.ToOffset(KstOffset);
        var currentHour = now.Hour;
        var currentDay = (int)now.DayOfWeek;
        var dateKey = now.ToString("yyyy-MM-dd");
        var trackingKey = $"{data.RoomId}:{dateKey}:{currentHour}";

        // Already sent for this room/date/hour
        if (_sentTracking.ContainsKey(trackingKey))
            return null;

        var filter = Builders<ScheduledMessage>.Filter.And(
            Builders<ScheduledMessage>.Filter.Eq(x => x.RoomId, data.RoomId),
            Builders<ScheduledMessage>.Filter.AnyEq(x => x.Hours, currentHour),
            Builders<ScheduledMessage>.Filter.AnyEq(x => x.Days, currentDay)
        );

        var messages = await _scheduledMessages.Find(filter).ToListAsync();
        if (messages.Count == 0)
            return null;

        // Mark as sent
        _sentTracking.TryAdd(trackingKey, 0);

        var combined = string.Join("\n\n━━━━━━━━━━━━━━━━━━\n\n",
            messages.Select(m => m.Message));

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = combined
        };
    }

    public async Task<List<ScheduledMessage>> GetScheduledMessagesAsync(string roomId)
    {
        var filter = Builders<ScheduledMessage>.Filter.Eq(x => x.RoomId, roomId);
        var sort = Builders<ScheduledMessage>.Sort.Ascending(x => x.CreatedAt);
        return await _scheduledMessages.Find(filter).Sort(sort).ToListAsync();
    }

    public async Task<bool> RemoveScheduledMessageAsync(string roomId, int displayIndex)
    {
        var messages = await GetScheduledMessagesAsync(roomId);
        if (displayIndex < 1 || displayIndex > messages.Count)
            return false;

        var target = messages[displayIndex - 1];
        var filter = Builders<ScheduledMessage>.Filter.Eq(x => x.Id, target.Id);
        var result = await _scheduledMessages.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<int> RemoveAllScheduledMessagesAsync(string roomId)
    {
        var filter = Builders<ScheduledMessage>.Filter.Eq(x => x.RoomId, roomId);
        var result = await _scheduledMessages.DeleteManyAsync(filter);
        return (int)result.DeletedCount;
    }

    public int CleanupExpiredSessions()
    {
        var now = DateTimeOffset.UtcNow;
        var expiredKeys = _sessions
            .Where(kvp => now - kvp.Value.LastActivityAt > SessionTimeout)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
            _sessions.TryRemove(key, out _);

        return expiredKeys.Count;
    }

    public int CleanupStaleSentTracking()
    {
        var todayKey = DateTimeOffset.UtcNow.ToOffset(KstOffset).ToString("yyyy-MM-dd");
        var staleKeys = _sentTracking.Keys
            .Where(k => !k.Contains(todayKey))
            .ToList();

        foreach (var key in staleKeys)
            _sentTracking.TryRemove(key, out _);

        return staleKeys.Count;
    }

    private static readonly Dictionary<string, DayOfWeek> DayNameMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["월"] = DayOfWeek.Monday,
        ["화"] = DayOfWeek.Tuesday,
        ["수"] = DayOfWeek.Wednesday,
        ["목"] = DayOfWeek.Thursday,
        ["금"] = DayOfWeek.Friday,
        ["토"] = DayOfWeek.Saturday,
        ["일"] = DayOfWeek.Sunday,
    };

    private static readonly List<int> AllDays = [0, 1, 2, 3, 4, 5, 6];

    private static string FormatDays(List<int> days)
    {
        if (days.Count == 7)
            return "전체";

        var dayNames = new[] { "일", "월", "화", "수", "목", "금", "토" };
        return string.Join(", ", days.Order().Select(d => dayNames[d]));
    }

    private enum SetupStage
    {
        AwaitingMessage,
        AwaitingDays,
        AwaitingHours
    }

    private sealed class SetupSession
    {
        public required string RoomId { get; init; }
        public required string SenderHash { get; init; }
        public required string SenderName { get; init; }
        public required string RoomName { get; init; }
        public SetupStage Stage { get; set; }
        public string? Message { get; set; }
        public List<int>? Days { get; set; }
        public DateTimeOffset LastActivityAt { get; set; }
    }
}
