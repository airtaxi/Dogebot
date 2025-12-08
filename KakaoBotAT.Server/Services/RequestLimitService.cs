using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class RequestLimitService : IRequestLimitService
{
    private readonly IMongoCollection<RoomRequestLimit> _roomLimits;
    private readonly IMongoCollection<RoomRequestLimitApprovalCode> _approvalCodes;
    private readonly IMongoCollection<UserDailyRequest> _userDailyRequests;
    private readonly IAdminService _adminService;
    private readonly Random _random = new();

    public RequestLimitService(IMongoDbService mongoDbService, IAdminService adminService)
    {
        _roomLimits = mongoDbService.Database.GetCollection<RoomRequestLimit>("roomRequestLimits");
        _approvalCodes = mongoDbService.Database.GetCollection<RoomRequestLimitApprovalCode>("roomRequestLimitApprovalCodes");
        _userDailyRequests = mongoDbService.Database.GetCollection<UserDailyRequest>("userDailyRequests");
        _adminService = adminService;
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var roomLimitIndexKeys = Builders<RoomRequestLimit>.IndexKeys.Ascending(x => x.RoomId);
        var roomLimitIndexModel = new CreateIndexModel<RoomRequestLimit>(roomLimitIndexKeys, new CreateIndexOptions { Unique = true });
        _roomLimits.Indexes.CreateOne(roomLimitIndexModel);

        var approvalCodeIndexKeys = Builders<RoomRequestLimitApprovalCode>.IndexKeys.Ascending(x => x.Code);
        var approvalCodeIndexModel = new CreateIndexModel<RoomRequestLimitApprovalCode>(approvalCodeIndexKeys);
        _approvalCodes.Indexes.CreateOne(approvalCodeIndexModel);

        var userDailyRequestIndexKeys = Builders<UserDailyRequest>.IndexKeys
            .Ascending(x => x.RoomId)
            .Ascending(x => x.SenderHash)
            .Ascending(x => x.Date);
        var userDailyRequestIndexModel = new CreateIndexModel<UserDailyRequest>(userDailyRequestIndexKeys, new CreateIndexOptions { Unique = true });
        _userDailyRequests.Indexes.CreateOne(userDailyRequestIndexModel);
    }

    public async Task<string> CreateLimitApprovalCodeAsync(string roomId, string roomName, int dailyLimit, string requestedBy)
    {
        var code = GenerateApprovalCode();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresAt = now + 600;

        var approvalCode = new RoomRequestLimitApprovalCode
        {
            Code = code,
            RoomId = roomId,
            RoomName = roomName,
            DailyLimit = dailyLimit,
            RequestedBy = requestedBy,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        await _approvalCodes.InsertOneAsync(approvalCode);

        return code;
    }

    public async Task<bool> ApproveLimitAsync(string code, string approverHash)
    {
        if (!await _adminService.IsAdminAsync(approverHash))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<RoomRequestLimitApprovalCode>.Filter.And(
            Builders<RoomRequestLimitApprovalCode>.Filter.Eq(x => x.Code, code),
            Builders<RoomRequestLimitApprovalCode>.Filter.Gt(x => x.ExpiresAt, now)
        );

        var approvalCode = await _approvalCodes.Find(filter).FirstOrDefaultAsync();
        if (approvalCode == null)
            return false;

        var roomLimit = new RoomRequestLimit
        {
            RoomId = approvalCode.RoomId,
            RoomName = approvalCode.RoomName,
            DailyLimit = approvalCode.DailyLimit,
            SetBy = approverHash,
            SetAt = now
        };

        var roomFilter = Builders<RoomRequestLimit>.Filter.Eq(x => x.RoomId, approvalCode.RoomId);
        await _roomLimits.ReplaceOneAsync(roomFilter, roomLimit, new ReplaceOptions { IsUpsert = true });

        await _approvalCodes.DeleteOneAsync(Builders<RoomRequestLimitApprovalCode>.Filter.Eq(x => x.Id, approvalCode.Id));

        return true;
    }

    public async Task<bool> RemoveLimitAsync(string roomId, string removerHash)
    {
        if (!await _adminService.IsAdminAsync(removerHash))
            return false;

        var filter = Builders<RoomRequestLimit>.Filter.Eq(x => x.RoomId, roomId);
        var result = await _roomLimits.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<bool> CheckRequestLimitAsync(string roomId, string senderHash)
    {
        // Admin users are not limited
        if (await _adminService.IsAdminAsync(senderHash))
            return true;

        var roomFilter = Builders<RoomRequestLimit>.Filter.Eq(x => x.RoomId, roomId);
        var roomLimit = await _roomLimits.Find(roomFilter).FirstOrDefaultAsync();

        if (roomLimit == null)
            return true;

        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var userFilter = Builders<UserDailyRequest>.Filter.And(
            Builders<UserDailyRequest>.Filter.Eq(x => x.RoomId, roomId),
            Builders<UserDailyRequest>.Filter.Eq(x => x.SenderHash, senderHash),
            Builders<UserDailyRequest>.Filter.Eq(x => x.Date, today)
        );

        var userRequest = await _userDailyRequests.Find(userFilter).FirstOrDefaultAsync();

        if (userRequest == null)
            return true;

        return userRequest.RequestCount < roomLimit.DailyLimit;
    }

    public async Task IncrementRequestCountAsync(string roomId, string senderHash)
    {
        // Admin users' requests are not counted
        if (await _adminService.IsAdminAsync(senderHash))
            return;

        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var filter = Builders<UserDailyRequest>.Filter.And(
            Builders<UserDailyRequest>.Filter.Eq(x => x.RoomId, roomId),
            Builders<UserDailyRequest>.Filter.Eq(x => x.SenderHash, senderHash),
            Builders<UserDailyRequest>.Filter.Eq(x => x.Date, today)
        );

        var update = Builders<UserDailyRequest>.Update
            .Inc(x => x.RequestCount, 1)
            .Set(x => x.LastRequestTime, now)
            .SetOnInsert(x => x.RoomId, roomId)
            .SetOnInsert(x => x.SenderHash, senderHash)
            .SetOnInsert(x => x.Date, today);

        await _userDailyRequests.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
    }

    public async Task<(bool HasLimit, int? DailyLimit, int? UsedToday)> GetLimitInfoAsync(string roomId, string senderHash)
    {
        var roomFilter = Builders<RoomRequestLimit>.Filter.Eq(x => x.RoomId, roomId);
        var roomLimit = await _roomLimits.Find(roomFilter).FirstOrDefaultAsync();

        if (roomLimit == null)
            return (false, null, null);

        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var userFilter = Builders<UserDailyRequest>.Filter.And(
            Builders<UserDailyRequest>.Filter.Eq(x => x.RoomId, roomId),
            Builders<UserDailyRequest>.Filter.Eq(x => x.SenderHash, senderHash),
            Builders<UserDailyRequest>.Filter.Eq(x => x.Date, today)
        );

        var userRequest = await _userDailyRequests.Find(userFilter).FirstOrDefaultAsync();
        var usedToday = userRequest?.RequestCount ?? 0;

        return (true, roomLimit.DailyLimit, usedToday);
    }

    private string GenerateApprovalCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
