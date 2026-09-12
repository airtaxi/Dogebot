using System.Security.Cryptography;
using System.Text;
using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class DengAiLongReplyService : IDengAiLongReplyService, IDengAiCallableService
{
    private const string BaseUrlEnvironmentVariableName = "DOGEBOT_PUBLIC_BASE_URL";
    private static readonly TimeSpan s_retentionPeriod = TimeSpan.FromDays(3);

    private readonly IMongoCollection<DengAiLongReply> _longReplies;
    private readonly IMongoCollection<DengAiLongReplySetting> _settings;
    private readonly ILogger<DengAiLongReplyService> _logger;
    private readonly string? _baseUrl;

    public DengAiLongReplyService(IMongoDbService mongoDbService, ILogger<DengAiLongReplyService> logger)
    {
        _longReplies = mongoDbService.Database.GetCollection<DengAiLongReply>("dengAiLongReplies");
        _settings = mongoDbService.Database.GetCollection<DengAiLongReplySetting>("dengAiLongReplySettings");
        _logger = logger;
        _baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariableName)?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(_baseUrl)) _logger.LogWarning("[DENG_AI_LINK] {EnvironmentVariableName} is not set. Long reply links will not be generated.", BaseUrlEnvironmentVariableName);
        CreateIndexes();
    }

    public bool IsBaseUrlConfigured => !string.IsNullOrWhiteSpace(_baseUrl);

    private void CreateIndexes()
    {
        var urlHashIndexKeys = Builders<DengAiLongReply>.IndexKeys.Ascending(reply => reply.UrlHash);
        var urlHashIndexModel = new CreateIndexModel<DengAiLongReply>(urlHashIndexKeys, new CreateIndexOptions { Unique = true });
        _longReplies.Indexes.CreateOne(urlHashIndexModel);

        var expireAtIndexKeys = Builders<DengAiLongReply>.IndexKeys.Ascending(reply => reply.ExpireAt);
        var expireAtIndexModel = new CreateIndexModel<DengAiLongReply>(expireAtIndexKeys, new CreateIndexOptions { ExpireAfter = TimeSpan.Zero });
        _longReplies.Indexes.CreateOne(expireAtIndexModel);

        var roomIdIndexKeys = Builders<DengAiLongReplySetting>.IndexKeys.Ascending(setting => setting.RoomId);
        var roomIdIndexModel = new CreateIndexModel<DengAiLongReplySetting>(roomIdIndexKeys, new CreateIndexOptions { Unique = true });
        _settings.Indexes.CreateOne(roomIdIndexModel);
    }

    public async Task<bool> IsEnabledAsync(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId)) return false;

        var filter = Builders<DengAiLongReplySetting>.Filter.Eq(setting => setting.RoomId, roomId);
        var setting = await _settings.Find(filter).FirstOrDefaultAsync();
        return setting?.IsEnabled ?? false;
    }

    public async Task SetEnabledAsync(string roomId, string roomName, bool enabled, string updatedBy)
    {
        var currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<DengAiLongReplySetting>.Filter.Eq(setting => setting.RoomId, roomId);
        var update = Builders<DengAiLongReplySetting>.Update
            .Set(setting => setting.RoomId, roomId)
            .Set(setting => setting.RoomName, roomName)
            .Set(setting => setting.IsEnabled, enabled)
            .Set(setting => setting.UpdatedBy, updatedBy)
            .Set(setting => setting.UpdatedAt, currentUnixTime);

        await _settings.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
    }

    public async Task<string?> StoreAndGetUrlAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl)) return null;

        var sha512Bytes = SHA512.HashData(Encoding.UTF8.GetBytes(content));
        var sha512Hash = ComputeHash(sha512Bytes);
        var existing = await _longReplies.Find(reply => reply.Id == sha512Hash).FirstOrDefaultAsync();
        if (existing is not null) return BuildUrl(existing.UrlHash);

        var maximumAttemptCount = sha512Bytes.Length / DengAiLongReplyUrlHash.ByteLength;
        for (var attempt = 0; attempt < maximumAttemptCount; attempt++)
        {
            var urlHash = DengAiLongReplyUrlHash.Create(sha512Bytes, attempt);
            var longReply = new DengAiLongReply
            {
                Id = sha512Hash,
                UrlHash = urlHash,
                Content = content,
                ExpireAt = DateTime.UtcNow.Add(s_retentionPeriod)
            };

            try { await _longReplies.InsertOneAsync(longReply); }
            catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
            {
                // A concurrent request may have stored the same reply. If not, the short url hash collided with another reply, so retry with the next slice.
                var concurrent = await _longReplies.Find(reply => reply.Id == sha512Hash).FirstOrDefaultAsync();
                if (concurrent is not null) return BuildUrl(concurrent.UrlHash);
                continue;
            }

            return BuildUrl(urlHash);
        }

        _logger.LogError("[DENG_AI_LINK] Failed to allocate a unique url hash for a long reply after {AttemptCount} attempts", maximumAttemptCount);
        return null;
    }

    public async Task<string?> GetContentByUrlHashAsync(string urlHash)
    {
        var longReply = await _longReplies.Find(reply => reply.UrlHash == urlHash).FirstOrDefaultAsync();
        return longReply?.Content;
    }

    public string? GetReplyUrl(string urlHash) => string.IsNullOrWhiteSpace(_baseUrl) ? null : BuildUrl(urlHash);

    private string BuildUrl(string urlHash) => $"{_baseUrl}/deng/{urlHash}";

    private static string ComputeHash(byte[] hashBytes) => Convert.ToHexString(hashBytes).ToLowerInvariant();

    #region Deng AI callable service

    IReadOnlyList<DengAiToolDefinition> IDengAiCallableService.GetDengAiTools() =>
    [
        new("get_long_reply_link_mode", "Check whether the current chat room uses 댕댕링크 mode, where AI replies of 80 characters or longer are delivered as a link instead of the full text. " + "Call this when the user asks whether long replies are currently sent as links, or when the current state is unclear before changing it.", DengAiJsonSchema.Object()),
        new("set_long_reply_link_mode",
            "Turn 댕댕링크 mode on or off for the current chat room. When it is on, AI replies of 80 characters or longer are delivered as a link instead of the full text; when it is off, every reply is sent as plain text. "
            + "Call this whenever the user asks to change how long replies are delivered in this room, for example: \"앞으로 링크로 보내지 마\", \"답변을 링크 말고 그대로 보여줘\", \"링크 말고 원문으로 보여줘\", \"긴 답변은 링크로 보내줘\", \"링크로 정리해서 보내줘\". "
            + "Set enabled to true to send long replies as links, or false to always send the full text. "
            + "The change is stored on the server for this room and applies to every later reply, including the reply that confirms this change, so after calling this tool tell the user that the room setting has been updated. "
            + "Do not call this tool for questions that only ask about the current state; use get_long_reply_link_mode for those.",
            DengAiJsonSchema.Object(new Dictionary<string, DengAiJsonSchemaProperty>
            {
                ["enabled"] = DengAiJsonSchemaProperty.Boolean("True to deliver long AI replies as links, false to always deliver the full text.")
            }, ["enabled"]))
    ];

    async Task<string> IDengAiCallableService.ExecuteDengAiToolAsync(string toolName, string arguments, DengAiToolContext context, CancellationToken cancellationToken)
    {
        return toolName switch
        {
            "get_long_reply_link_mode" => DengAiToolJson.Serialize(new { IsEnabled = await IsEnabledAsync(context.RoomId) }),
            "set_long_reply_link_mode" => await CreateSetLongReplyLinkModeToolResultAsync(arguments, context),
            _ => "Unknown long reply link tool."
        };
    }

    private async Task<string> CreateSetLongReplyLinkModeToolResultAsync(string arguments, DengAiToolContext context)
    {
        var enabled = DengAiToolJson.ReadBoolean(arguments, "enabled");
        if (enabled is null) return "The 'enabled' argument must be true or false.";

        await SetEnabledAsync(context.RoomId, context.RoomName, enabled.Value, context.SenderHash);

        if (_logger.IsEnabled(LogLevel.Warning)) _logger.LogWarning("[DENG_AI_LINK] Long reply link mode set to {IsEnabled} for room {RoomName} by Deng AI request from {SenderName}", enabled.Value, context.RoomName, context.SenderName);

        return DengAiToolJson.Serialize(new
        {
            IsEnabled = enabled.Value,
            LinkAvailable = IsBaseUrlConfigured,
            Message = CreateLongReplyLinkModeMessage(enabled.Value)
        });
    }

    private string CreateLongReplyLinkModeMessage(bool enabled)
    {
        if (!enabled) return "댕댕링크 모드가 비활성화되었습니다. 이제 긴 답변도 원문 그대로 전송됩니다.";
        if (IsBaseUrlConfigured) return "댕댕링크 모드가 활성화되었습니다. 이제 80자 이상의 답변이 링크로 전송됩니다.";
        return "댕댕링크 모드가 활성화되었지만 서버에 공개 주소가 설정되어 있지 않아 링크가 생성되지 않습니다.";
    }

    #endregion
}