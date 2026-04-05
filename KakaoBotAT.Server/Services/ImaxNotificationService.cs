using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using KakaoBotAT.Commons;
using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class ImaxNotificationService : IImaxNotificationService
{
    private readonly IMongoCollection<ImaxNotification> _imaxNotifications;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ImaxNotificationService> _logger;

    private const string ImaxApiBaseUrl = "https://imax.kagamine-rin.com";
    private static readonly TimeSpan KstOffset = TimeSpan.FromHours(9);
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<(string RoomId, string SenderHash), SetupSession> _sessions = new();

    private static readonly (string Code, string Name)[] Regions =
    [
        ("01", "서울"),
        ("02", "경기"),
        ("03", "인천"),
        ("04", "강원"),
        ("05", "대전/충청"),
        ("06", "대구"),
        ("07", "부산/울산"),
        ("08", "경상"),
        ("09", "광주/전라"),
    ];

    public ImaxNotificationService(
        IMongoDbService mongoDbService,
        IHttpClientFactory httpClientFactory,
        ILogger<ImaxNotificationService> logger)
    {
        _imaxNotifications = mongoDbService.Database.GetCollection<ImaxNotification>("imaxNotifications");
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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

    #region Session Management

    public void StartSession(string roomId, string senderHash, string senderName, string roomName,
        ImaxSessionType type = ImaxSessionType.Setup, string? movieSearchQuery = null)
    {
        var session = new SetupSession
        {
            RoomId = roomId,
            SenderHash = senderHash,
            SenderName = senderName,
            RoomName = roomName,
            Type = type,
            Stage = SetupStage.AwaitingRegion,
            LastActivityAt = DateTimeOffset.UtcNow,
            MovieSearchQuery = movieSearchQuery
        };

        _sessions[(roomId, senderHash)] = session;
    }

    public async Task<ServerResponse?> HandleSessionInputAsync(KakaoMessageData data)
    {
        var key = (data.RoomId, data.SenderHash);
        if (!_sessions.TryGetValue(key, out var session))
            return null;

        if (DateTimeOffset.UtcNow - session.LastActivityAt > SessionTimeout)
        {
            _sessions.TryRemove(key, out _);
            return null;
        }

        var trimmed = data.Content.Trim();

        if (trimmed.Equals("!취소", StringComparison.OrdinalIgnoreCase))
        {
            _sessions.TryRemove(key, out _);
            var cancelMessage = session.Type switch
            {
                ImaxSessionType.Setup => "❌ IMAX 알림 설정이 취소되었습니다.",
                ImaxSessionType.ScheduleQuery => "❌ IMAX 시간표 조회가 취소되었습니다.",
                ImaxSessionType.MovieList => "❌ 영화 목록 조회가 취소되었습니다.",
                _ => "❌ 취소되었습니다."
            };
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = cancelMessage
            };
        }

        // Let other ! commands pass through to normal command routing
        if (trimmed.StartsWith('!'))
            return null;

        session.LastActivityAt = DateTimeOffset.UtcNow;

        return session.Stage switch
        {
            SetupStage.AwaitingRegion => await HandleAwaitingRegionAsync(session, data),
            SetupStage.AwaitingTheater => await HandleAwaitingTheaterAsync(session, data),
            SetupStage.AwaitingMovie => HandleAwaitingMovie(session, data),
            SetupStage.AwaitingDate => await HandleAwaitingDateAsync(session, data),
            SetupStage.AwaitingMovieQuery => await HandleAwaitingMovieQueryAsync(session, data),
            _ => null
        };
    }

    private async Task<ServerResponse> HandleAwaitingRegionAsync(SetupSession session, KakaoMessageData data)
    {
        var trimmed = data.Content.Trim();
        if (!int.TryParse(trimmed, out var regionIndex) || regionIndex < 1 || regionIndex > Regions.Length)
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"❌ 1~{Regions.Length} 사이의 숫자를 입력해주세요.\n\n❌ 취소: !취소"
            };
        }

        var selectedRegion = Regions[regionIndex - 1];

        var httpClient = _httpClientFactory.CreateClient();
        try
        {
            var url = $"{ImaxApiBaseUrl}/theaters?regionCode={selectedRegion.Code}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ 영화관 목록 조회 실패 (HTTP {(int)response.StatusCode})\n\n❌ 취소: !취소"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            var document = JsonSerializer.Deserialize<JsonElement>(json);
            var theaters = document.GetProperty("theaters");

            if (theaters.GetArrayLength() == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ {selectedRegion.Name} 지역에 영화관이 없습니다.\n\n다른 지역 번호를 입력해주세요.\n\n❌ 취소: !취소"
                };
            }

            var theaterList = theaters.EnumerateArray()
                .Select(theater => (
                    SiteNumber: theater.GetProperty("siteNumber").GetString()!,
                    SiteName: theater.GetProperty("siteName").GetString()!))
                .ToList();

            session.AvailableTheaters = theaterList;
            session.Stage = SetupStage.AwaitingTheater;

            var builder = new StringBuilder();
            builder.AppendLine($"✅ 지역: {selectedRegion.Name}");
            builder.AppendLine();
            builder.AppendLine("영화관을 선택해주세요:");
            for (int index = 0; index < theaterList.Count; index++)
                builder.AppendLine($"  {index + 1,2}. {theaterList[index].SiteName}");
            builder.AppendLine();
            builder.AppendLine("❌ 취소: !취소");

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = builder.ToString().TrimEnd()
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[IMAX_SET] Failed to fetch theater list");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 영화관 목록 조회 중 오류가 발생했습니다.\n\n❌ 취소: !취소"
            };
        }
    }

    private async Task<ServerResponse> HandleAwaitingTheaterAsync(SetupSession session, KakaoMessageData data)
    {
        var trimmed = data.Content.Trim();
        if (!int.TryParse(trimmed, out var theaterIndex) || theaterIndex < 1 || theaterIndex > session.AvailableTheaters!.Count)
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"❌ 1~{session.AvailableTheaters!.Count} 사이의 숫자를 입력해주세요.\n\n❌ 취소: !취소"
            };
        }

        var selectedTheater = session.AvailableTheaters![theaterIndex - 1];
        session.SelectedSiteNumber = selectedTheater.SiteNumber;
        session.SelectedSiteName = selectedTheater.SiteName;

        // Branch based on session type after theater selection
        if (session.Type == ImaxSessionType.MovieList)
            return await FetchAndDisplayMovieListAsync(session, data);

        if (session.Type == ImaxSessionType.ScheduleQuery)
        {
            if (!string.IsNullOrEmpty(session.MovieSearchQuery))
                return await SearchAndDisplayScheduleAsync(session, data, session.MovieSearchQuery);

            session.Stage = SetupStage.AwaitingMovieQuery;
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"✅ 영화관: CGV {selectedTheater.SiteName}\n\n" +
                          "조회할 영화 이름을 입력해주세요.\n\n" +
                          "❌ 취소: !취소"
            };
        }

        // Setup type: fetch movies for selection
        var httpClient = _httpClientFactory.CreateClient();
        try
        {
            var url = $"{ImaxApiBaseUrl}/movies?siteNo={selectedTheater.SiteNumber}";
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ 영화 목록 조회 실패 (HTTP {(int)response.StatusCode})\n\n❌ 취소: !취소"
                };
            }

            var json = await response.Content.ReadAsStringAsync();
            var document = JsonSerializer.Deserialize<JsonElement>(json);
            var movies = document.GetProperty("movies");

            if (movies.GetArrayLength() == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ CGV {selectedTheater.SiteName}에서 현재 상영 중인 영화가 없습니다.\n\n❌ 취소: !취소"
                };
            }

            var movieList = movies.EnumerateArray()
                .Select(movie => (
                    MovieNumber: movie.GetProperty("movieNumber").GetString()!,
                    MovieName: movie.GetProperty("movieName").GetString()!))
                .ToList();

            session.AvailableMovies = movieList;
            session.Stage = SetupStage.AwaitingMovie;

            var builder = new StringBuilder();
            builder.AppendLine($"✅ 영화관: CGV {selectedTheater.SiteName}");
            builder.AppendLine();
            builder.AppendLine("영화를 선택해주세요:");
            for (int index = 0; index < movieList.Count; index++)
                builder.AppendLine($"  {index + 1,2}. {movieList[index].MovieName}");
            builder.AppendLine();
            builder.AppendLine("❌ 취소: !취소");

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = builder.ToString().TrimEnd()
            };
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "[IMAX_SET] Failed to fetch movie list");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 영화 목록 조회 중 오류가 발생했습니다.\n\n❌ 취소: !취소"
            };
        }
    }

    private ServerResponse HandleAwaitingMovie(SetupSession session, KakaoMessageData data)
    {
        var trimmed = data.Content.Trim();
        if (!int.TryParse(trimmed, out var movieIndex) || movieIndex < 1 || movieIndex > session.AvailableMovies!.Count)
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"❌ 1~{session.AvailableMovies!.Count} 사이의 숫자를 입력해주세요.\n\n❌ 취소: !취소"
            };
        }

        var selectedMovie = session.AvailableMovies![movieIndex - 1];
        session.SelectedMovieNumber = selectedMovie.MovieNumber;
        session.SelectedMovieName = selectedMovie.MovieName;
        session.Stage = SetupStage.AwaitingDate;

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = data.RoomId,
            Message = $"✅ 영화: {selectedMovie.MovieName}\n\n" +
                      "날짜를 입력해주세요 (yyyyMMdd)\n" +
                      "예: 20260405\n\n" +
                      "선택적으로 /키워드를 뒤에 붙일 수 있습니다.\n" +
                      "예: 20260405/IMAX알림\n\n" +
                      "❌ 취소: !취소"
        };
    }

    private async Task<ServerResponse> HandleAwaitingDateAsync(SetupSession session, KakaoMessageData data)
    {
        var key = (session.RoomId, session.SenderHash);
        var input = data.Content.Trim();

        // Parse date and optional keyword (separated by /)
        var slashIndex = input.IndexOf('/');
        string dateString;
        string? keyword;
        if (slashIndex > 0)
        {
            dateString = input[..slashIndex].Trim();
            keyword = input[(slashIndex + 1)..].Trim();
            if (string.IsNullOrEmpty(keyword)) keyword = null;
        }
        else
        {
            dateString = input;
            keyword = null;
        }

        if (!DateTime.TryParseExact(dateString, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 날짜 형식이 올바르지 않습니다.\nyyyyMMdd 형식으로 입력하세요. (예: 20260405)\n\n❌ 취소: !취소"
            };
        }

        var kstNow = DateTimeOffset.UtcNow.ToOffset(KstOffset);
        if (parsedDate.Date < kstNow.Date)
        {
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 과거 날짜는 설정할 수 없습니다.\n\n❌ 취소: !취소"
            };
        }

        var (success, message) = await RegisterAsync(
            session.RoomId, dateString, session.SelectedMovieName!, session.SelectedMovieNumber!,
            session.SelectedSiteNumber!, session.SelectedSiteName!, keyword,
            session.SenderHash, session.SenderName, session.RoomName);

        _sessions.TryRemove(key, out _);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[IMAX_SET] {Result} by {Sender} in room {RoomName} for {Movie} on {Date} at {Site}",
                success ? "Registered" : "Failed", session.SenderName, session.RoomName,
                session.SelectedMovieName, dateString, session.SelectedSiteName);

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

    private async Task<ServerResponse> FetchAndDisplayMovieListAsync(SetupSession session, KakaoMessageData data)
    {
        var key = (session.RoomId, session.SenderHash);
        var httpClient = _httpClientFactory.CreateClient();

        try
        {
            var url = string.IsNullOrEmpty(session.MovieSearchQuery)
                ? $"{ImaxApiBaseUrl}/movies?siteNo={session.SelectedSiteNumber}"
                : $"{ImaxApiBaseUrl}/movies?siteNo={session.SelectedSiteNumber}&query={Uri.EscapeDataString(session.MovieSearchQuery)}";

            var response = await httpClient.GetAsync(url);
            _sessions.TryRemove(key, out _);

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
                var noResultMessage = string.IsNullOrEmpty(session.MovieSearchQuery)
                    ? $"ℹ️ CGV {session.SelectedSiteName}에서 현재 상영 중인 영화가 없습니다."
                    : $"ℹ️ \"{session.MovieSearchQuery}\"에 해당하는 영화를 찾을 수 없습니다.";

                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = noResultMessage
                };
            }

            var header = string.IsNullOrEmpty(session.MovieSearchQuery)
                ? $"🎬 CGV {session.SelectedSiteName} 상영 영화 목록"
                : $"🎬 CGV {session.SelectedSiteName} \"{session.MovieSearchQuery}\" 검색 결과";

            var movieList = string.Join("\n",
                movies.EnumerateArray().Select(m => $"  • {m.GetProperty("movieName").GetString()}"));

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[MOVIE_LIST] Showing {Count} movies at CGV {Site} for {Sender} in room {RoomName}",
                    movies.GetArrayLength(), session.SelectedSiteName, session.SenderName, session.RoomName);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = $"{header}\n\n{movieList}\n\n총 {movies.GetArrayLength()}편"
            };
        }
        catch (Exception exception)
        {
            _sessions.TryRemove(key, out _);
            _logger.LogError(exception, "[MOVIE_LIST] Failed to fetch movie list for CGV {Site}", session.SelectedSiteName);
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 영화 목록 조회 중 오류가 발생했습니다."
            };
        }
    }

    private async Task<ServerResponse> SearchAndDisplayScheduleAsync(SetupSession session, KakaoMessageData data, string movieQuery)
    {
        var key = (session.RoomId, session.SenderHash);
        var httpClient = _httpClientFactory.CreateClient();

        try
        {
            // Search for the movie
            var movieSearchUrl = $"{ImaxApiBaseUrl}/movies?siteNo={session.SelectedSiteNumber}&query={Uri.EscapeDataString(movieQuery)}";
            var movieResponse = await httpClient.GetAsync(movieSearchUrl);

            if (!movieResponse.IsSuccessStatusCode)
            {
                _sessions.TryRemove(key, out _);
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
                session.Stage = SetupStage.AwaitingMovieQuery;
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ \"{movieQuery}\"에 해당하는 영화를 찾을 수 없습니다.\n\n" +
                              "다른 영화 이름을 입력해주세요.\n\n" +
                              "❌ 취소: !취소"
                };
            }

            if (movies.GetArrayLength() > 1)
            {
                session.Stage = SetupStage.AwaitingMovieQuery;
                var movieList = string.Join("\n",
                    movies.EnumerateArray().Select(m => $"  • {m.GetProperty("movieName").GetString()}"));
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"❌ \"{movieQuery}\"에 해당하는 영화가 여러 개 있습니다.\n\n" +
                              $"{movieList}\n\n" +
                              "더 구체적인 이름으로 검색해주세요.\n\n" +
                              "❌ 취소: !취소"
                };
            }

            // Single match found - fetch today's schedule
            var movie = movies[0];
            var movieName = movie.GetProperty("movieName").GetString()!;
            var movieNumber = movie.GetProperty("movieNumber").GetString()!;

            var todayKst = DateTimeOffset.UtcNow.ToOffset(KstOffset).ToString("yyyyMMdd");
            var scheduleUrl = $"{ImaxApiBaseUrl}/schedule?siteNo={session.SelectedSiteNumber}&date={todayKst}&movNo={movieNumber}";

            HttpResponseMessage scheduleResponse;
            try
            {
                scheduleResponse = await httpClient.GetAsync(scheduleUrl);
            }
            catch (Exception exception)
            {
                _sessions.TryRemove(key, out _);
                _logger.LogError(exception, "[IMAX_QUERY] Failed to fetch schedule");
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 시간표 조회 중 오류가 발생했습니다."
                };
            }

            _sessions.TryRemove(key, out _);

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

            var scheduleJson = await scheduleResponse.Content.ReadAsStringAsync();
            var scheduleDocument = JsonSerializer.Deserialize<JsonElement>(scheduleJson);
            var screenings = scheduleDocument.GetProperty("screenings");

            if (screenings.GetArrayLength() == 0)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = $"ℹ️ 오늘({todayKst}) CGV {session.SelectedSiteName}\n\n" +
                              $"🎬 {movieName}\n\n" +
                              "상영 스케줄이 없습니다."
                };
            }

            var hasImax = scheduleDocument.GetProperty("hasImax").GetBoolean();
            var imaxCount = scheduleDocument.GetProperty("imaxCount").GetInt32();
            var totalCount = scheduleDocument.GetProperty("totalCount").GetInt32();

            var builder = new StringBuilder();
            builder.AppendLine($"🎬 오늘({todayKst}) CGV {session.SelectedSiteName}");
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

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[IMAX_QUERY] {Sender} queried {Movie} at CGV {Site} in room {RoomName}: IMAX={HasImax}",
                    session.SenderName, movieName, session.SelectedSiteName, session.RoomName, hasImax);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = builder.ToString().TrimEnd()
            };
        }
        catch (Exception exception)
        {
            _sessions.TryRemove(key, out _);
            _logger.LogError(exception, "[IMAX_QUERY] Error in schedule query session");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "❌ 시간표 조회 중 오류가 발생했습니다."
            };
        }
    }

    private Task<ServerResponse> HandleAwaitingMovieQueryAsync(SetupSession session, KakaoMessageData data) =>
        SearchAndDisplayScheduleAsync(session, data, data.Content.Trim());

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

    private enum SetupStage
    {
        AwaitingRegion,
        AwaitingTheater,
        AwaitingMovie,
        AwaitingDate,
        AwaitingMovieQuery
    }

    private sealed class SetupSession
    {
        public required string RoomId { get; init; }
        public required string SenderHash { get; init; }
        public required string SenderName { get; init; }
        public required string RoomName { get; init; }
        public required ImaxSessionType Type { get; init; }
        public SetupStage Stage { get; set; }
        public List<(string SiteNumber, string SiteName)>? AvailableTheaters { get; set; }
        public string? SelectedSiteNumber { get; set; }
        public string? SelectedSiteName { get; set; }
        public List<(string MovieNumber, string MovieName)>? AvailableMovies { get; set; }
        public string? SelectedMovieNumber { get; set; }
        public string? SelectedMovieName { get; set; }
        public string? MovieSearchQuery { get; set; }
        public DateTimeOffset LastActivityAt { get; set; }
    }

    #endregion

    #region Notification CRUD

    public async Task<(bool Success, string Message)> RegisterAsync(
        string roomId, string screeningDate, string movieName, string movieNumber,
        string siteNumber, string siteName, string? keyword,
        string senderHash, string senderName, string roomName)
    {
        var existing = await GetNotificationAsync(roomId);
        if (existing is not null)
        {
            var existingDateDisplay = FormatScreeningDate(existing.ScreeningDate);
            return (false, $"❌ 이 방에 이미 알림이 등록되어 있습니다.\n\n" +
                          $"🎬 {existing.MovieName}\n" +
                          $"📅 기존 알림: {existingDateDisplay}\n\n" +
                          $"!아이맥스해제 후 다시 등록해주세요.");
        }

        var notification = new ImaxNotification
        {
            RoomId = roomId,
            ScreeningDate = screeningDate,
            MovieName = movieName,
            MovieNumber = movieNumber,
            SiteNumber = siteNumber,
            SiteName = siteName,
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
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return (false, "❌ 이 방에 이미 알림이 등록되어 있습니다.\n!아이맥스해제 후 다시 등록해주세요.");
        }

        var dateDisplay = FormatScreeningDate(screeningDate);
        var keywordDisplay = string.IsNullOrEmpty(keyword) ? "" : $"\n🔑 키워드: {keyword}";
        return (true, $"✅ IMAX 알림이 등록되었습니다!\n\n" +
                      $"🏢 영화관: CGV {siteName}\n" +
                      $"🎬 영화: {movieName}\n" +
                      $"📅 날짜: {dateDisplay}{keywordDisplay}\n" +
                      $"⏰ 5~10초 간격으로 IMAX 상영 여부를 확인합니다.\n\n" +
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

    public async Task<ServerResponse?> CheckAndDeliverForRoomsAsync(IEnumerable<string> roomIds)
    {
        var roomIdList = roomIds.ToList();
        if (roomIdList.Count == 0)
            return null;

        var filter = Builders<ImaxNotification>.Filter.And(
            Builders<ImaxNotification>.Filter.In(x => x.RoomId, roomIdList),
            Builders<ImaxNotification>.Filter.Ne(x => x.PendingMessage, null));

        // Atomically find and delete: prevents duplicate delivery
        var notification = await _imaxNotifications.FindOneAndDeleteAsync(filter);

        if (notification is null)
            return null;

        return new ServerResponse
        {
            Action = "send_text",
            RoomId = notification.RoomId,
            Message = notification.PendingMessage!
        };
    }

    public async Task<int> CleanupExpiredNotificationsAsync()
    {
        var kstNow = DateTimeOffset.UtcNow.ToOffset(KstOffset);

        // Only delete notifications whose screening date's next day 3:00 AM KST has passed
        // (to account for late-night screenings)
        string cutoffDateString;
        if (kstNow.Hour >= 3)
            cutoffDateString = kstNow.AddDays(-1).ToString("yyyyMMdd");
        else
            cutoffDateString = kstNow.AddDays(-2).ToString("yyyyMMdd");

        var filter = Builders<ImaxNotification>.Filter.Lte(x => x.ScreeningDate, cutoffDateString);
        var result = await _imaxNotifications.DeleteManyAsync(filter);
        return (int)result.DeletedCount;
    }

    #endregion

    public static string FormatScreeningDate(string yyyyMMdd)
    {
        if (DateTime.TryParseExact(yyyyMMdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return $"{date.Year}년 {date.Month}월 {date.Day}일";
        return yyyyMMdd;
    }
}
