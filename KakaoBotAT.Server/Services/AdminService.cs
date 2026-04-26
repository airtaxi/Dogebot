using KakaoBotAT.Server.Models;
using MongoDB.Driver;

namespace KakaoBotAT.Server.Services;

public class AdminService : IAdminService
{
    private readonly IMongoCollection<AdminUser> _adminUsers;
    private readonly IMongoCollection<AdminApprovalCode> _approvalCodes;
    private readonly Random _random = new();

    public string ChiefAdminHash => "7df4a497868641e1de7bdac030efdabaca6fbcb52fe600ce58077d357d240900";

    public AdminService(IMongoDbService mongoDbService)
    {
        _adminUsers = mongoDbService.Database.GetCollection<AdminUser>("adminUsers");
        _approvalCodes = mongoDbService.Database.GetCollection<AdminApprovalCode>("adminApprovalCodes");
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        var adminUserIndexKeys = Builders<AdminUser>.IndexKeys.Ascending(x => x.SenderHash);
        var adminUserIndexModel = new CreateIndexModel<AdminUser>(adminUserIndexKeys, new CreateIndexOptions { Unique = true });
        _adminUsers.Indexes.CreateOne(adminUserIndexModel);

        var approvalCodeIndexKeys = Builders<AdminApprovalCode>.IndexKeys.Ascending(x => x.Code);
        var approvalCodeIndexModel = new CreateIndexModel<AdminApprovalCode>(approvalCodeIndexKeys);
        _approvalCodes.Indexes.CreateOne(approvalCodeIndexModel);
    }

    public async Task<bool> IsAdminAsync(string senderHash)
    {
        if (senderHash == ChiefAdminHash)
            return true;

        var filter = Builders<AdminUser>.Filter.Eq(x => x.SenderHash, senderHash);
        return await _adminUsers.Find(filter).AnyAsync();
    }

    public async Task<string> CreateApprovalCodeAsync(string senderHash, string senderName, string roomId, string roomName)
    {
        var code = GenerateApprovalCode();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var expiresAt = now + 600;

        var approvalCode = new AdminApprovalCode
        {
            Code = code,
            SenderHash = senderHash,
            SenderName = senderName,
            RoomId = roomId,
            RoomName = roomName,
            CreatedAt = now,
            ExpiresAt = expiresAt
        };

        await _approvalCodes.InsertOneAsync(approvalCode);

        return code;
    }

    public async Task<bool> ApproveAdminAsync(string code, string approverHash)
    {
        if (approverHash != ChiefAdminHash)
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<AdminApprovalCode>.Filter.And(
            Builders<AdminApprovalCode>.Filter.Eq(x => x.Code, code),
            Builders<AdminApprovalCode>.Filter.Gt(x => x.ExpiresAt, now)
        );

        var approvalCode = await _approvalCodes.Find(filter).FirstOrDefaultAsync();
        if (approvalCode == null)
            return false;

        if (approvalCode.SenderHash == ChiefAdminHash)
            return false;

        var adminUser = new AdminUser
        {
            SenderHash = approvalCode.SenderHash,
            SenderName = approvalCode.SenderName,
            RoomId = approvalCode.RoomId,
            RoomName = approvalCode.RoomName,
            AddedBy = approverHash,
            AddedAt = now
        };

        try
        {
            await _adminUsers.InsertOneAsync(adminUser);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }

        await _approvalCodes.DeleteOneAsync(Builders<AdminApprovalCode>.Filter.Eq(x => x.Id, approvalCode.Id));

        return true;
    }

    public async Task<bool> RemoveAdminAsync(string senderHash, string removerHash)
    {
        if (removerHash != ChiefAdminHash)
            return false;

        if (senderHash == ChiefAdminHash)
            return false;

        var filter = Builders<AdminUser>.Filter.Eq(x => x.SenderHash, senderHash);
        var result = await _adminUsers.DeleteOneAsync(filter);
        return result.DeletedCount > 0;
    }

    public async Task<List<(string RoomName, string SenderName, string SenderHash, long AddedAt)>> GetAdminListAsync()
    {
        var sort = Builders<AdminUser>.Sort.Ascending(x => x.RoomName).Ascending(x => x.SenderName);
        var admins = await _adminUsers.Find(FilterDefinition<AdminUser>.Empty).Sort(sort).ToListAsync();
        
        return admins.Select(a => (a.RoomName, a.SenderName, a.SenderHash, a.AddedAt)).ToList();
    }

    public async Task<int> DeleteExpiredApprovalCodesAsync()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var filter = Builders<AdminApprovalCode>.Filter.Lte(x => x.ExpiresAt, now);
        var result = await _approvalCodes.DeleteManyAsync(filter);
        return (int)result.DeletedCount;
    }

    private string GenerateApprovalCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, 6)
            .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
