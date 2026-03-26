using System.Text.Json;
using KakaoBotAT.Commons;

namespace KakaoBotAT.Server.Commands;

public class CgvMovieListCommandHandler(
    IHttpClientFactory httpClientFactory,
    ILogger<CgvMovieListCommandHandler> logger) : ICommandHandler
{
    private const string ImaxApiBaseUrl = "https://imax.kagamine-rin.com";

    public string Command => "!영화목록";

    public bool CanHandle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Equals(Command, StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith($"{Command} ", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var parts = data.Content.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            var query = parts.Length > 1 ? parts[1].Trim() : null;

            var httpClient = httpClientFactory.CreateClient();
            var url = string.IsNullOrWhiteSpace(query)
                ? $"{ImaxApiBaseUrl}/movies"
                : $"{ImaxApiBaseUrl}/movies?query={Uri.EscapeDataString(query)}";

            HttpResponseMessage response;
            try
            {
                response = await httpClient.GetAsync(url);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[CGV_MOVIES] Failed to connect to IMAX API");
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ IMAX API 서버에 연결할 수 없습니다."
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ 영화 목록 조회 실패 (HTTP {(int)response.StatusCode})"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            var document = JsonSerializer.Deserialize<JsonElement>(json);
            var movies = document.GetProperty("movies");

            if (movies.GetArrayLength() == 0)
            {
                var noResultMessage = string.IsNullOrWhiteSpace(query)
                    ? "ℹ️ 현재 용산아이파크몰 CGV에서 상영 중인 영화가 없습니다."
                    : $"ℹ️ \"{query}\"에 해당하는 영화를 찾을 수 없습니다.";

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = noResultMessage
                };
            }

            var header = string.IsNullOrWhiteSpace(query)
                ? "🎬 용산아이파크몰 CGV 상영 영화 목록"
                : $"🎬 \"{query}\" 검색 결과";

            var movieList = string.Join("\n",
                movies.EnumerateArray().Select(m => $"  • {m.GetProperty("movieName").GetString()}"));

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[CGV_MOVIES] Showing {Count} movies to {Sender} in room {RoomName}",
                    movies.GetArrayLength(), data.SenderName, data.RoomName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"{header}\n\n{movieList}\n\n총 {movies.GetArrayLength()}편"
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CGV_MOVIES] Error processing CGV movie list command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "영화 목록 조회 중 오류가 발생했습니다."
            };
        }
    }
}
