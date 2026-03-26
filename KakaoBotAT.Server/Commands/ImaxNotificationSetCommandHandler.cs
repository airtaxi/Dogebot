using System.Globalization;
using System.Text.Json;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Services;

namespace KakaoBotAT.Server.Commands;

public class ImaxNotificationSetCommandHandler(
    IImaxNotificationService imaxNotificationService,
    IAdminService adminService,
    IHttpClientFactory httpClientFactory,
    ILogger<ImaxNotificationSetCommandHandler> logger) : ICommandHandler
{
    private const string ImaxApiBaseUrl = "https://imax.kagamine-rin.com";

    public string Command => "!용아맥설정";

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
            if (!await adminService.IsAdminAsync(data.SenderHash))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "⛔ 권한이 없습니다. 관리자만 용아맥 알림을 설정할 수 있습니다."
                };
            }

            var parts = data.Content.Trim().Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 3)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 사용법: !용아맥설정 (날짜) (영화이름) [/구문]\n\n" +
                             "예: !용아맥설정 20260330 프로젝트 헤일메리\n" +
                             "예: !용아맥설정 20260330 프로젝트 헤일메리 /IMAX알림\n\n" +
                             "날짜는 yyyyMMdd 형식으로 입력하세요.\n" +
                             "[/구문]은 카카오톡 키워드 알림용이며 생략 가능합니다."
                };
            }

            var dateStr = parts[1];
            if (!DateTime.TryParseExact(dateStr, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 날짜 형식이 올바르지 않습니다.\nyyyyMMdd 형식으로 입력하세요. (예: 20260330)"
                };
            }

            var kstNow = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9));
            if (parsedDate.Date < kstNow.Date)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 과거 날짜는 설정할 수 없습니다."
                };
            }

            // Parse movie name and optional keyword (separated by /)
            var afterDate = parts[2];
            var slashIndex = afterDate.LastIndexOf('/');
            string movieQuery;
            string? keyword;
            if (slashIndex > 0)
            {
                movieQuery = afterDate[..slashIndex].Trim();
                keyword = afterDate[(slashIndex + 1)..].Trim();
                if (string.IsNullOrEmpty(keyword)) keyword = null;
            }
            else
            {
                movieQuery = afterDate.Trim();
                keyword = null;
            }

            if (string.IsNullOrWhiteSpace(movieQuery))
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 영화 이름을 입력해주세요."
                };
            }

            // Search for the movie via IMAX API
            var httpClient = httpClientFactory.CreateClient();
            var encodedQuery = Uri.EscapeDataString(movieQuery);
            var movieSearchUrl = $"{ImaxApiBaseUrl}/movies?query={encodedQuery}";

            HttpResponseMessage movieResponse;
            try
            {
                movieResponse = await httpClient.GetAsync(movieSearchUrl);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[IMAX_SET] Failed to connect to IMAX API");
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
            var movieDoc = JsonSerializer.Deserialize<JsonElement>(movieJson);
            var movies = movieDoc.GetProperty("movies");

            if (movies.GetArrayLength() == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ \"{movieQuery}\"에 해당하는 영화를 찾을 수 없습니다.\n\n" +
                             "현재 용산아이파크몰 CGV에서 상영 중인 영화만 검색 가능합니다."
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

            var (success, message) = await imaxNotificationService.RegisterAsync(
                data.RoomId, dateStr, movieName, movieNumber, keyword,
                data.SenderHash, data.SenderName, data.RoomName);

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[IMAX_SET] {Result} by {Sender} in room {RoomName} for {Movie} on {Date}",
                    success ? "Registered" : "Failed", data.SenderName, data.RoomName, movieName, dateStr);

            // In personal chat, skip reply on success to preserve reply capability for the actual notification
            if (success && !data.IsGroupChat)
                return new ServerResponse();

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[IMAX_SET] Error processing IMAX notification set command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "용아맥 알림 설정 중 오류가 발생했습니다."
            };
        }
    }
}
