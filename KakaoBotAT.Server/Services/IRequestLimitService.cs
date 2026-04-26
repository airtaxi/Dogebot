namespace KakaoBotAT.Server.Services;

public interface IRequestLimitService
{
    Task<bool> SetLimitAsync(string roomId, string roomName, int dailyLimit, string setBy);
    Task<bool> RemoveLimitAsync(string roomId, string removerHash);
    Task<bool> CheckRequestLimitAsync(string roomId, string senderHash);
    Task IncrementRequestCountAsync(string roomId, string senderHash);
    Task<(bool HasLimit, int? DailyLimit, int? UsedToday)> GetLimitInfoAsync(string roomId, string senderHash);
    Task<int> DeleteExpiredApprovalCodesAsync();
}
