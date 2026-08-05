using System.Globalization;
using System.Text.Json.Nodes;
using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class LeaveWorkService : ILeaveWorkService, IDengAiCallableService
{
    private const string HolidayApiKeyEnvironmentVariableName = "DOGEBOT_HOLIDAY_API_KEY";
    private const string HolidayApiBaseUrl = "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getHoliDeInfo";
    private const int MinimumLeaveWorkHour = 16;
    private const int MaximumLeaveWorkHour = 20;
    private const double AfterWorkWittyMessageProbability = 0.03;
    private static readonly TimeSpan s_koreaStandardTimeOffset = TimeSpan.FromHours(9);

    private static readonly string[] s_leaveTimeFormats =
    [
        "오늘 퇴근 시각: {0}시!",
        "🐶 오늘 퇴근은 {0}시! 조금만 더 힘내자!",
        "퇴근 시간 뽑았다... 오늘은 {0}시다!",
        "오늘의 퇴근 럭키타임: {0}시!",
        "퇴근 카운트다운... {0}시까지 힘내자!"
    ];

    private static readonly string[] s_weekendMessages =
    [
        "{0}에 출근한 사람이 있겠냐! 주말엔 퇴근 시간이고 뭐고 이불과 함께하는 게 맞다.",
        "{0}에 퇴근을 물어보는 거야? 지금 주말인 거 알지? 퇴근은커녕 출근 자체를 하면 안 되는 날이다.",
        "{0}에 출근한 사람이 있겠냐... 퇴근 시간을 묻는 것 자체가 이미 쉬고 있다는 증거다.",
        "{0}에 출근한 사람이 있겠냐... 오늘은 기상 그 자체가 이미 승리인 날이다.",
        "{0}에 퇴근 시간을 물어보다니... 주말에는 시간이라는 개념이 흐르지 않는다."
    ];

    private static readonly string[] s_holidayMessages =
    [
        "{0}에 출근한 사람이 있겠냐! 오늘은 무슨 수를 써도 쉬는 날이다.",
        "{0}에 출근한 사람이 있겠냐... 어른도 다 쉬는 날인데 뭘 물어보는 거냐.",
        "{0}에 퇴근 시간을 물어보다니... 오늘은 그냥 즐기러 가는 게 맞다.",
        "{0}에 출근한 사람이 있겠냐... 오늘은 회사 걱정을 접어둘 수 있는 날이다.",
        "{0}인데 퇴근을 물어보는 거냐? 오늘은 하루 종일 나만의 시간이다!"
    ];

    private static readonly string[] s_afterWorkMessages =
    [
        "이미 퇴근하셨습니다!",
        "이미 퇴근하셨습니다. 지금은 자유 시간입니다.",
        "퇴근은 이미 완료됐습니다. 오늘 일은 수고하셨습니다!",
        "이미 퇴근하셨습니다! 오늘도 고생 많으셨습니다.",
        "이미 퇴근하셨습니다... 이제는 퇴근 걱정 없이 하루를 마무리할 시간입니다."
    ];

    private static readonly string[] s_afterWorkWittyMessages =
    [
        "이미 퇴근하셨습니다... 아니, 퇴근한 지가 한참 지났는데요? 지금 시각이 몇 시인지 아시는 거냐?",
        "퇴근? 이미 오래전에 하셨습니다! 지금쯤이면 집에서 뒹굴고 계실 시간인데, 혹시 회사에 계신 건 아니죠?",
        "이미 퇴근하셨습니다... 회사에 남아 계시다면, 그건 퇴근이 아니라 야근입니다!",
        "이미 퇴근하셨습니다... 오늘 출근 자체를 안 하셨다면, 더더욱 퇴근은 끝난 겁니다!",
        "퇴근은 이미 끝났습니다! 혹시 지금이 아침이라고 착각하신 거라면, 그건 시계 문제가 아니라 현실 문제입니다."
    ];

    private readonly HttpClient _httpClient;
    private readonly string? _holidayApiKey;
    private readonly IMongoCollection<HolidayMonthRecord> _holidayMonthRecords;
    private readonly IMongoCollection<DailyLeaveWorkRecord> _dailyLeaveWorkRecords;
    private readonly ILogger<LeaveWorkService> _logger;

    public LeaveWorkService(IHttpClientFactory httpClientFactory, ILogger<LeaveWorkService> logger, IMongoDbService mongoDbService)
    {
        _httpClient = httpClientFactory.CreateClient();
        _holidayApiKey = Environment.GetEnvironmentVariable(HolidayApiKeyEnvironmentVariableName);
        _holidayMonthRecords = mongoDbService.Database.GetCollection<HolidayMonthRecord>("holidayMonths");
        _dailyLeaveWorkRecords = mongoDbService.Database.GetCollection<DailyLeaveWorkRecord>("dailyLeaveWorkRecords");
        _logger = logger;
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var indexKeys = Builders<HolidayMonthRecord>.IndexKeys.Ascending(record => record.YearMonth);
        var indexModel = new CreateIndexModel<HolidayMonthRecord>(indexKeys, new CreateIndexOptions { Unique = true });
        _holidayMonthRecords.Indexes.CreateOne(indexModel);

        var dailyRecordIndexKeys = Builders<DailyLeaveWorkRecord>.IndexKeys
            .Ascending(record => record.SenderHash)
            .Ascending(record => record.Date);
        var dailyRecordIndexModel = new CreateIndexModel<DailyLeaveWorkRecord>(dailyRecordIndexKeys, new CreateIndexOptions { Unique = true });
        _dailyLeaveWorkRecords.Indexes.CreateOne(dailyRecordIndexModel);
    }

    public async Task<bool> HasDrawnTodayAsync(string senderHash)
    {
        var today = DateTimeOffset.UtcNow.ToOffset(s_koreaStandardTimeOffset).ToString("yyyy-MM-dd");
        var filter = Builders<DailyLeaveWorkRecord>.Filter.Eq(record => record.SenderHash, senderHash) &
                     Builders<DailyLeaveWorkRecord>.Filter.Eq(record => record.Date, today);
        return await _dailyLeaveWorkRecords.Find(filter).AnyAsync();
    }

    public async Task RecordDrawAsync(string senderHash)
    {
        var today = DateTimeOffset.UtcNow.ToOffset(s_koreaStandardTimeOffset).ToString("yyyy-MM-dd");
        var record = new DailyLeaveWorkRecord
        {
            SenderHash = senderHash,
            Date = today
        };
        await _dailyLeaveWorkRecords.InsertOneAsync(record);
    }

    public async Task<string> CreateLeaveWorkMessageAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.ToOffset(s_koreaStandardTimeOffset);

        var holidayName = await GetTodayHolidayNameAsync(now, cancellationToken);
        if (holidayName is not null) return string.Format(CultureInfo.InvariantCulture, PickRandom(s_holidayMessages), holidayName);

        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return string.Format(CultureInfo.InvariantCulture, PickRandom(s_weekendMessages), GetKoreanDayName(now.DayOfWeek));

        var currentHour = now.Hour;
        if (currentHour >= 5 && currentHour <= MaximumLeaveWorkHour)
        {
            var leaveHour = Random.Shared.Next(Math.Max(currentHour, MinimumLeaveWorkHour), MaximumLeaveWorkHour + 1);
            return string.Format(CultureInfo.InvariantCulture, PickRandom(s_leaveTimeFormats), leaveHour);
        }

        if (Random.Shared.NextDouble() < AfterWorkWittyMessageProbability) return PickRandom(s_afterWorkWittyMessages);
        return PickRandom(s_afterWorkMessages);
    }

    private async Task<string?> GetTodayHolidayNameAsync(DateTimeOffset koreaNow, CancellationToken cancellationToken)
    {
        var yearMonth = koreaNow.ToString("yyyyMM", CultureInfo.InvariantCulture);
        var today = koreaNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var existingRecord = await _holidayMonthRecords
            .Find(record => record.YearMonth == yearMonth)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingRecord is not null)
            return existingRecord.Holidays.FirstOrDefault(holiday => holiday.Date == today)?.Name;

        var holidayNames = await FetchHolidayNamesAsync(koreaNow, cancellationToken);
        if (holidayNames is null) return null;

        var record = new HolidayMonthRecord
        {
            YearMonth = yearMonth,
            Holidays = [.. holidayNames.Select(entry => new HolidayMonthRecord.HolidayEntry { Date = entry.Key, Name = entry.Value })]
        };
        await _holidayMonthRecords.ReplaceOneAsync(
            record => record.YearMonth == yearMonth,
            record,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        return holidayNames.GetValueOrDefault(today);
    }

    private async Task<IReadOnlyDictionary<string, string>?> FetchHolidayNamesAsync(DateTimeOffset koreaNow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_holidayApiKey))
        {
            _logger.LogError("[LEAVE_WORK] Holiday API key is not configured. Environment variable: {EnvironmentVariableName}", HolidayApiKeyEnvironmentVariableName);
            return null;
        }

        try
        {
            var url = $"{HolidayApiBaseUrl}?serviceKey={Uri.EscapeDataString(_holidayApiKey)}&solYear={koreaNow.Year}&solMonth={koreaNow.Month:D2}&_type=json&numOfRows=20";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("[LEAVE_WORK] Holiday API request failed with status code {StatusCode}", response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = JsonNode.Parse(content) as JsonObject;
            var root = document?["response"] as JsonObject;
            var header = root?["header"] as JsonObject;
            if (root is null || header is null || header["resultCode"]?.GetValue<string>() != "00")
            {
                _logger.LogError("[LEAVE_WORK] Holiday API returned an error result");
                return null;
            }

            var items = root["body"]?["items"];
            if (items is not JsonObject itemsObject || itemsObject["item"] is null) return new Dictionary<string, string>(StringComparer.Ordinal);

            var holidayNames = new Dictionary<string, string>(StringComparer.Ordinal);
            if (itemsObject["item"] is JsonArray itemArray)
            {
                foreach (var item in itemArray) TryAddHolidayName(item as JsonObject, holidayNames);
            }
            else if (itemsObject["item"] is JsonObject itemObject)
            {
                TryAddHolidayName(itemObject, holidayNames);
            }

            return holidayNames;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[LEAVE_WORK] Error fetching holiday information");
            return null;
        }
    }

    private static void TryAddHolidayName(JsonObject? item, Dictionary<string, string> holidayNames)
    {
        if (item is null) return;
        if (item["isHoliday"]?.GetValue<string>() != "Y") return;
        if (item["locdate"]?.GetValue<int>() is not int locdate) return;

        var holidayName = item["dateName"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(holidayName)) return;

        if (holidayName == "부처님오신날") holidayName = "석가탄신일";
        else if (holidayName.StartsWith("전국동시", StringComparison.Ordinal)) holidayName = holidayName["전국동시".Length..];

        holidayNames[locdate.ToString(CultureInfo.InvariantCulture)] = holidayName;
    }

    private static string GetKoreanDayName(DayOfWeek dayOfWeek) =>
        dayOfWeek switch
        {
            DayOfWeek.Sunday => "일요일",
            DayOfWeek.Monday => "월요일",
            DayOfWeek.Tuesday => "화요일",
            DayOfWeek.Wednesday => "수요일",
            DayOfWeek.Thursday => "목요일",
            DayOfWeek.Friday => "금요일",
            DayOfWeek.Saturday => "토요일",
            _ => dayOfWeek.ToString()
        };

    private static string PickRandom(string[] messages) =>
        messages[Random.Shared.Next(messages.Length)];

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_leave_work_time", "Tell the user when they can leave work today with a playful message. Each user can draw only once per day (KST). Returns the suggested leave time on weekdays, and a witty message on weekends or holidays.", DengAiJsonSchema.Object())
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        if (!toolName.Equals("get_leave_work_time", StringComparison.Ordinal)) return "Unknown leave work tool.";
        if (string.IsNullOrWhiteSpace(context.SenderHash)) return "사용자 식별 정보가 없어 퇴근 시간을 확인할 수 없습니다.";
        if (await HasDrawnTodayAsync(context.SenderHash)) return "오늘의 퇴근 시간은 이미 확인했습니다. 내일 다시 확인해주세요.";

        var message = await CreateLeaveWorkMessageAsync(cancellationToken);
        await RecordDrawAsync(context.SenderHash);
        return message;
    }

    #endregion
}
