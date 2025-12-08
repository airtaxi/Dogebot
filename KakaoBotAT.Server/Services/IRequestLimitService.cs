namespace KakaoBotAT.Server.Services;

public interface IRequestLimitService
{
    Task<string> CreateLimitApprovalCodeAsync(string roomId, string roomName, int dailyLimit, string requestedBy);
    Task<bool> ApproveLimitAsync(string code, string approverHash);
    Task<bool> RemoveLimitAsync(string roomId, string removerHash);
    Task<bool> CheckRequestLimitAsync(string roomId, string senderHash);
    Task IncrementRequestCountAsync(string roomId, string senderHash);
    Task<(bool HasLimit, int? DailyLimit, int? UsedToday)> GetLimitInfoAsync(string roomId, string senderHash);
}
