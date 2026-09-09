using System.Security.Cryptography;
using System.Text;
using Dogebot.Server.Models;
using MongoDB.Driver;

namespace Dogebot.Server.Services;

public class DengAiLongReplyService : IDengAiLongReplyService
{
    private const string BaseUrlEnvironmentVariableName = "DOGEBOT_PUBLIC_BASE_URL";
    private static readonly TimeSpan s_retentionPeriod = TimeSpan.FromDays(3);

    private readonly IMongoCollection<DengAiLongReply> _longReplies;
    private readonly string? _baseUrl;

    public DengAiLongReplyService(IMongoDbService mongoDbService, ILogger<DengAiLongReplyService> logger)
    {
        _longReplies = mongoDbService.Database.GetCollection<DengAiLongReply>("dengAiLongReplies");
        _baseUrl = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariableName)?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(_baseUrl)) logger.LogWarning("[DENG_AI_LINK] {EnvironmentVariableName} is not set. Long reply links will not be generated.", BaseUrlEnvironmentVariableName);
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
    }

    public async Task<string?> StoreAndGetUrlAsync(string content)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl)) return null;

        var sha512Hash = ComputeHash(SHA512.HashData(Encoding.UTF8.GetBytes(content)));
        var existing = await _longReplies.Find(reply => reply.Id == sha512Hash).FirstOrDefaultAsync();
        if (existing is not null) return BuildUrl(existing.UrlHash);

        var urlHash = ComputeHash(MD5.HashData(Encoding.UTF8.GetBytes(sha512Hash)));
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
            // A concurrent request already stored the same reply.
            var concurrent = await _longReplies.Find(reply => reply.UrlHash == urlHash).FirstOrDefaultAsync();
            if (concurrent is not null) return BuildUrl(concurrent.UrlHash);
        }

        return BuildUrl(urlHash);
    }

    public async Task<string?> GetContentByUrlHashAsync(string urlHash)
    {
        var longReply = await _longReplies.Find(reply => reply.UrlHash == urlHash).FirstOrDefaultAsync();
        return longReply?.Content;
    }

    private string BuildUrl(string urlHash) => $"{_baseUrl}/deng/{urlHash}";

    private static string ComputeHash(byte[] hashBytes) => Convert.ToHexString(hashBytes).ToLowerInvariant();
}