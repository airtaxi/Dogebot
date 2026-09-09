using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !한강 command to show the latest Han River water quality data.
/// </summary>
public class HanRiverWaterQualityCommandHandler(IHanRiverWaterQualityService hanRiverWaterQualityService, ILogger<HanRiverWaterQualityCommandHandler> logger) : ICommandHandler
{
    public string Command => "!한강";

    public bool CanHandle(string content) =>
        content.Trim().StartsWith(Command, StringComparison.OrdinalIgnoreCase);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var waterQualityRows = await hanRiverWaterQualityService.GetLatestWaterQualityAsync();
            if (waterQualityRows is null || waterQualityRows.Count == 0) return new ServerResponse { Action = "send_text", RoomId = data.RoomId, Message = "❌ 한강 수질 정보를 가져올 수 없습니다." };

            logger.LogInformation("[HAN_RIVER] Water quality info requested by {Sender} in room {RoomId}: {StationCount} stations", data.SenderName, data.RoomId, waterQualityRows.Count);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = HanRiverWaterQualityService.FormatWaterQualityMessage(waterQualityRows)
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[HAN_RIVER] Error processing command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "한강 수질 정보 조회 중 오류가 발생했습니다."
            };
        }
    }
}