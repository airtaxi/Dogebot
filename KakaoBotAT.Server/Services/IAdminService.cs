namespace KakaoBotAT.Server.Services;

public interface IAdminService
{
    string ChiefAdminHash { get; }
    Task<bool> IsAdminAsync(string senderHash);
    Task<string> CreateApprovalCodeAsync(string senderHash, string senderName, string roomId, string roomName);
    Task<bool> ApproveAdminAsync(string code, string approverHash);
    Task<bool> RemoveAdminAsync(string senderHash, string removerHash);
    Task<List<(string RoomName, string SenderName, string SenderHash, long AddedAt)>> GetAdminListAsync();
}
