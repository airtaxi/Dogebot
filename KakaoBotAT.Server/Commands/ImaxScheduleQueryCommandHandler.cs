using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ImaxScheduleQueryCommandHandler(
    IAdminService adminService,
    IHttpClientFactory httpClientFactory,
    ILogger<ImaxScheduleQueryCommandHandler> logger) : ICommandHandler
{
    private const string ImaxApiBaseUrl = "https://imax.kagamine-rin.com";
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(1);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> RoomCooldowns = new();

    public string Command => "!용아맥조회";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 사용법: !용아맥조회 (영화이름)\n\n" +
                             "예: !용아맥조회 프로젝트 헤일메리\n\n" +
                             "오늘 용산아이파크몰 CGV IMAX 시간표를 조회합니다."
                };
            }

            // Check per-room cooldown (admin exempt)
            var isAdmin = await adminService.IsAdminAsync(data.SenderHash);
            if (!isAdmin)
            {
                if (RoomCooldowns.TryGetValue(data.RoomId, out var lastUsed))
                {
                    var remaining = CooldownDuration - (DateTimeOffset.UtcNow - lastUsed);
                    if (remaining > TimeSpan.Zero)
                    {
                        return new ServerResponse
                        {
                            Action = "send_text",
                            RoomId = data.RoomId,
                            Message = $"⏳ 이 방에서 {remaining.Seconds}초 후에 다시 사용할 수 있습니다."
                        };
                    }
                }
            }

            var movieQuery = parts[1].Trim();
            var httpClient = httpClientFactory.CreateClient();

            // Search movie
            var movieSearchUrl = $"{ImaxApiBaseUrl}/movies?query={Uri.EscapeDataString(movieQuery)}";
            HttpResponseMessage movieResponse;
            try
            {
                movieResponse = await httpClient.GetAsync(movieSearchUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[IMAX_QUERY] Failed to connect to IMAX API");
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ IMAX API 서버에 연결할 수 없습니다."
                };
            }

            if (!movieResponse.IsSuccessStatusCode)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ 영화 검색 실패 (HTTP {(int)movieResponse.StatusCode})"
                };
            }

            var movieJson = await movieResponse.Content.ReadAsStringAsync();
            var movieDocument = JsonSerializer.Deserialize<JsonElement>(movieJson);
            var movies = movieDocument.GetProperty("movies");

            if (movies.GetArrayLength() == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ \"{movieQuery}\"에 해당하는 영화를 찾을 수 없습니다.\n\n" +
                             "!영화목록 명령어로 상영 중인 영화를 확인하세요."
                };
            }

            if (movies.GetArrayLength() > 1)
            {
                var movieList = string.Join("\n",
                    movies.EnumerateArray().Select(m => $"  • {m.GetProperty("movieName").GetString()}"));
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ \"{movieQuery}\"에 해당하는 영화가 여러 개 있습니다.\n\n" +
                             $"{movieList}\n\n" +
                             "더 구체적인 이름으로 검색해주세요."
                };
            }

            var movie = movies[0];
            var movieName = movie.GetProperty("movieName").GetString()!;
            var movieNumber = movie.GetProperty("movieNumber").GetString()!;

            // Fetch today's schedule
            var todayKst = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("yyyyMMdd");
            var scheduleUrl = $"{ImaxApiBaseUrl}/schedule?date={todayKst}&movNo={movieNumber}";

            HttpResponseMessage scheduleResponse;
            try
            {
                scheduleResponse = await httpClient.GetAsync(scheduleUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[IMAX_QUERY] Failed to fetch schedule");
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 시간표 조회 중 오류가 발생했습니다."
                };
            }

            if ((int)scheduleResponse.StatusCode == 401)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ CGV API 인증 실패 (401)"
                };
            }

            if (!scheduleResponse.IsSuccessStatusCode)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ 시간표 조회 실패 (HTTP {(int)scheduleResponse.StatusCode})"
                };
            }

            // Record cooldown after successful API call
            RoomCooldowns[data.RoomId] = DateTimeOffset.UtcNow;

            var scheduleJson = await scheduleResponse.Content.ReadAsStringAsync();
            var scheduleDocument = JsonSerializer.Deserialize<JsonElement>(scheduleJson);
            var screenings = scheduleDocument.GetProperty("screenings");

            if (screenings.GetArrayLength() == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"ℹ️ 오늘({todayKst}) 용산아이파크몰 CGV\n\n" +
                             $"🎬 {movieName}\n\n" +
                             "상영 스케줄이 없습니다."
                };
            }

            var hasImax = scheduleDocument.GetProperty("hasImax").GetBoolean();
            var imaxCount = scheduleDocument.GetProperty("imaxCount").GetInt32();
            var totalCount = scheduleDocument.GetProperty("totalCount").GetInt32();

            var builder = new StringBuilder();
            builder.AppendLine($"🎬 오늘({todayKst}) 용산아이파크몰 CGV");
            builder.AppendLine($"🎬 {movieName}");
            builder.AppendLine();

            if (hasImax)
                builder.AppendLine($"✅ IMAX {imaxCount}회차 포함 (전체 {totalCount}회)");
            else
                builder.AppendLine($"❌ IMAX 없음 (전체 {totalCount}회)");

            builder.AppendLine();

            foreach (var screening in screenings.EnumerateArray())
            {
                var startTime = screening.GetProperty("startTime").GetString();
                var endTime = screening.GetProperty("endTime").GetString();
                var format = screening.GetProperty("format").GetString();
                var screenName = screening.GetProperty("screenName").GetString();
                var freeSeats = screening.GetProperty("freeSeats").GetInt32();
                var totalSeats = screening.GetProperty("totalSeats").GetInt32();
                var isImax = screening.GetProperty("isImax").GetBoolean();

                var imaxTag = isImax ? "🟢" : "⚪";
                builder.AppendLine($"{imaxTag} {startTime}~{endTime} | {format}");
                builder.AppendLine($"   {screenName} | 잔여 {freeSeats}/{totalSeats}석");
            }

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[IMAX_QUERY] {Sender} queried {Movie} in room {RoomName}: IMAX={HasImax}",
                    data.SenderName, movieName, data.RoomName, hasImax);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = builder.ToString().TrimEnd()
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[IMAX_QUERY] Error processing IMAX schedule query command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "시간표 조회 중 오류가 발생했습니다."
            };
        }
    }
}
