using KakaoBotAT.Commons;
using KakaoBotAT.Server.Commands;
using System.Diagnostics;

namespace KakaoBotAT.Server.Services;

/// <summary>
/// Service implementation that handles bot logic.
/// </summary>
public class KakaoService(
    ILogger<KakaoService> logger,
    CommandHandlerFactory commandHandlerFactory,
    IChatStatisticsService chatStatisticsService,
    IRequestLimitService requestLimitService,
    IScheduledMessageService scheduledMessageService,
    IImaxNotificationService imaxNotificationService,
    DebugLogService debugLogService) : IKakaoService
{

    /// <summary>
    /// Processes received notifications and executes appropriate command handlers.
    /// </summary>
    public async Task<ServerResponse> HandleNotificationAsync(ServerNotification notification)
    {
        var data = notification.Data;

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("[NOTIFY] Received from Room: {RoomName}, Sender: {SenderName}, Content: {Content}", 
                data.RoomName, data.SenderName, data.Content);

        // Record message statistics
        await chatStatisticsService.RecordMessageAsync(data);

        // Check for active scheduled message setup sessions
        var sessionResponse = await scheduledMessageService.HandleSessionInputAsync(data);
        if (sessionResponse is not null)
            return sessionResponse;

        // Check for active IMAX notification setup sessions
        var imaxSessionResponse = await imaxNotificationService.HandleSessionInputAsync(data);
        if (imaxSessionResponse is not null)
            return imaxSessionResponse;

        // Find and execute appropriate command handler
        var handler = commandHandlerFactory.FindHandler(data.Content);
        if (handler != null)
        {
            // Check if the command should be rate-limited
            // Exclude limit management commands and admin commands from rate limiting
            var isAdminCommand = handler.Command == "!제한설정" || 
                                handler.Command == "!제한해제" ||
                                handler.Command == "!관리추가" ||
                                handler.Command == "!관리제거" ||
                                handler.Command == "!관리목록" ||
                                handler.Command == "!랭크활성화" ||
                                handler.Command == "!랭크비활성화" ||
                                handler.Command == "!심삭제" ||
                                handler.Command == "!반복설정" ||
                                handler.Command == "!반복해제" ||
                                handler.Command == "!반복목록" ||
                                handler.Command == "!방백업" ||
                                handler.Command == "!방복원" ||
                                handler.Command == "!아이맥스설정" ||
                                handler.Command == "!아이맥스해제" ||
                                handler.Command == "!아이맥스목록" ||
                                handler.Command == "!디버그";

            if (!isAdminCommand)
            {
                // Check request limit
                var canExecute = await requestLimitService.CheckRequestLimitAsync(data.RoomId, data.SenderHash);

                if (!canExecute)
                {
                    var limitInfo = await requestLimitService.GetLimitInfoAsync(data.RoomId, data.SenderHash);

                    if (logger.IsEnabled(LogLevel.Warning))
                        logger.LogWarning("[REQUEST_LIMIT] User {Sender} exceeded daily limit in room {RoomName}",
                            data.SenderName, data.RoomName);

                    return new ServerResponse
                    {
                        Action = "send_text",
                        RoomId = data.RoomId,
                        Message = $"⛔ 하루 요청 제한 초과\n\n" +
                                 $"오늘 사용: {limitInfo.UsedToday}회\n" +
                                 $"일일 제한: {limitInfo.DailyLimit}회\n\n" +
                                 $"내일 다시 시도해주세요."
                    };
                }

                // Increment request count
                await requestLimitService.IncrementRequestCountAsync(data.RoomId, data.SenderHash);
            }

            return await handler.HandleAsync(data);
        }

        // Check for IMAX notifications to deliver
        var imaxResponse = await imaxNotificationService.CheckAndDeliverAsync(data);
        if (imaxResponse is not null)
        {
            debugLogService.Log("NOTIFY", $"IMAX 알림 전달 → {data.RoomName}");
            return imaxResponse;
        }

        // Check for scheduled messages to trigger
        var scheduledResponse = await scheduledMessageService.CheckAndSendScheduledMessageAsync(data);
        if (scheduledResponse is not null)
        {
            debugLogService.Log("NOTIFY", $"반복메시지 전달 (fallback) → {data.RoomName}");
            return scheduledResponse;
        }

        return new ServerResponse();
    }

    /// <summary>
    /// Retrieves queued commands. Checks for due IMAX notifications and scheduled messages
    /// for rooms where the client has reply actions available.
    /// </summary>
    public async Task<ServerResponse> GetPendingCommandAsync(IEnumerable<string> availableRoomIds)
    {
        var roomIds = availableRoomIds.ToList();
        if (roomIds.Count == 0)
            return new ServerResponse();

        debugLogService.Log("POLL", $"폴링 수신: {roomIds.Count}개 방");

        // Check IMAX first (higher priority - time-sensitive)
        var imaxResponse = await imaxNotificationService.CheckAndDeliverForRoomsAsync(roomIds);
        if (imaxResponse is not null)
        {
            debugLogService.Log("POLL", $"IMAX 알림 전달 (proactive) → {imaxResponse.RoomId}");
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[COMMAND] Delivering IMAX notification to room {RoomId}", imaxResponse.RoomId);
            return imaxResponse;
        }

        // Check scheduled messages
        var scheduledResponse = await scheduledMessageService.CheckAndSendScheduledMessageForRoomsAsync(roomIds);
        if (scheduledResponse is not null)
        {
            debugLogService.Log("POLL", $"반복메시지 전달 (proactive) → {scheduledResponse.RoomId}");
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[COMMAND] Delivering scheduled message to room {RoomId}", scheduledResponse.RoomId);
            return scheduledResponse;
        }

        return new ServerResponse();
    }
}