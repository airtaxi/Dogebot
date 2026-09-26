using Dogebot.Commons;
using Dogebot.LocoClient.Configuration;
using Dogebot.LocoClient.Models;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Dogebot.LocoClient.Services;

/// <summary>
/// Bridges the kakao-cli API mode server and Dogebot.Server.
/// Incoming room messages are forwarded to /notify, and /command is polled for queued deliveries.
/// </summary>
public class LocoBridgeService(ILocoCliApiClient cliApiClient, IDogebotServerApiClient serverApiClient, IOptions<LocoClientOptions> options, ILogger<LocoBridgeService> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<string, LocoRoom> _rooms = [];
    private readonly ConcurrentDictionary<string, RoomSubscription> _roomSubscriptions = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("[LOCO_BRIDGE] Bridge started. cli={CliBaseUrl}, server={ServerBaseUrl}", options.Value.CliBaseUrl, options.Value.ServerBaseUrl);

        try { await Task.WhenAll(RunRoomSynchronizationLoopAsync(stoppingToken), RunCommandPollingLoopAsync(stoppingToken)); }
        catch (OperationCanceledException) { logger.LogInformation("[LOCO_BRIDGE] Bridge stopped."); }
        finally { await StopRoomSubscriptionsAsync(); }
    }

    private async Task RunRoomSynchronizationLoopAsync(CancellationToken cancellationToken)
    {
        var refreshInterval = TimeSpan.FromSeconds(Math.Max(5, options.Value.RoomRefreshIntervalSeconds));
        using var timer = new PeriodicTimer(refreshInterval);

        do await SynchronizeRoomsAsync(cancellationToken); while (await timer.WaitForNextTickAsync(cancellationToken));
    }

    private async Task SynchronizeRoomsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var rooms = await cliApiClient.GetRoomsAsync(cancellationToken);
            foreach (var room in rooms)
            {
                _rooms[room.Id] = room;
                EnsureRoomSubscription(room.Id, cancellationToken);
            }

            var removedRoomIds = _roomSubscriptions.Keys.Where(roomId => rooms.All(room => room.Id != roomId)).ToArray();
            foreach (var removedRoomId in removedRoomIds) await RemoveRoomSubscriptionAsync(removedRoomId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) { logger.LogWarning(exception, "[LOCO_BRIDGE] Failed to synchronize the room list from the CLI server."); }
    }

    private void EnsureRoomSubscription(string roomId, CancellationToken cancellationToken)
    {
        if (_roomSubscriptions.ContainsKey(roomId)) return;

        var subscriptionCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var subscriptionTask = Task.Run(() => RunRoomStreamAsync(roomId, subscriptionCancellationTokenSource.Token), CancellationToken.None);
        _roomSubscriptions[roomId] = new RoomSubscription(subscriptionCancellationTokenSource, subscriptionTask);

        logger.LogInformation("[LOCO_BRIDGE] Subscribed. room={RoomId}", roomId);
    }

    private async Task RemoveRoomSubscriptionAsync(string roomId)
    {
        if (!_roomSubscriptions.TryRemove(roomId, out var subscription)) return;

        subscription.CancellationTokenSource.Cancel();
        await subscription.StreamTask;
        subscription.CancellationTokenSource.Dispose();
        _rooms.TryRemove(roomId, out _);

        logger.LogInformation("[LOCO_BRIDGE] Unsubscribed. room={RoomId}", roomId);
    }

    private async Task StopRoomSubscriptionsAsync()
    {
        var streamTasks = new List<Task>();

        foreach (var (roomId, subscription) in _roomSubscriptions)
        {
            subscription.CancellationTokenSource.Cancel();
            streamTasks.Add(subscription.StreamTask);
            _roomSubscriptions.TryRemove(roomId, out _);
        }

        await Task.WhenAll(streamTasks);
    }

    private async Task RunRoomStreamAsync(string roomId, CancellationToken cancellationToken)
    {
        // OperationCanceledException means the room disappeared or the bridge is shutting down.
        try { await cliApiClient.RunRoomStreamAsync(roomId, (message, room) => OnRoomMessageReceivedAsync(message, room), cancellationToken); }
        catch (OperationCanceledException) { }
        catch (Exception exception) { logger.LogError(exception, "[LOCO_BRIDGE] Room stream stopped unexpectedly. room={RoomId}", roomId); }
    }

    private async Task OnRoomMessageReceivedAsync(LocoRoomMessage message, LocoRoom? room)
    {
        if (message.IsMine) return;

        var roomName = room?.Name ?? (_rooms.TryGetValue(message.RoomId, out var cachedRoom) ? cachedRoom.Name : message.RoomId);
        logger.LogInformation("[LOCO_NOTIFY] Room: {RoomName} / Sender: {SenderName} / Content: {Content}", roomName, message.Nickname, message.Text);

        try
        {
            var notification = new ServerNotification { Data = MapToKakaoMessageData(message, roomName) };
            var response = await serverApiClient.NotifyAsync(notification, CancellationToken.None);
            await ExecuteServerResponseAsync(response, CancellationToken.None);
        }
        catch (Exception exception) { logger.LogError(exception, "[LOCO_NOTIFY] Failed to process the incoming message. room={RoomId}", message.RoomId); }
    }

    private async Task RunCommandPollingLoopAsync(CancellationToken cancellationToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.CommandPollIntervalSeconds));
        using var timer = new PeriodicTimer(pollInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var roomIds = _rooms.Keys.ToArray();
            if (roomIds.Length == 0) continue;

            try
            {
                var response = await serverApiClient.GetPendingCommandAsync(roomIds, cancellationToken);
                await ExecuteServerResponseAsync(response, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogWarning(exception, "[LOCO_POLL] Command polling failed."); }
        }
    }

    private async Task ExecuteServerResponseAsync(ServerResponse response, CancellationToken cancellationToken)
    {
        List<ServerResponseItem> responseItems = response.Items.Count > 0 ? response.Items : [CreateSingleResponseItem(response)];

        foreach (var responseItem in responseItems)
        {
            try { await ExecuteResponseItemAsync(responseItem, cancellationToken); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogError(exception, "[LOCO_EXEC] Failed to execute a response item. action={Action}, room={RoomId}", responseItem.Action, responseItem.RoomId); }
        }
    }

    private async Task ExecuteResponseItemAsync(ServerResponseItem responseItem, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responseItem.Action)) return;

        if (responseItem.Action == "send_text")
        {
            await ExecuteSendTextAsync(responseItem, cancellationToken);
            return;
        }

        if (responseItem.Action == "read")
        {
            // Dogebot.Server does not emit read actions, and the kakao-cli API has no read endpoint.
            logger.LogDebug("[LOCO_EXEC] Ignoring read action. room={RoomId}", responseItem.RoomId);
            return;
        }

        if (responseItem.Action == "error")
        {
            logger.LogWarning("[LOCO_EXEC] Server returned an error action: {Message}", responseItem.Message);
            return;
        }

        logger.LogDebug("[LOCO_EXEC] Unsupported action ignored. action={Action}", responseItem.Action);
    }

    private async Task ExecuteSendTextAsync(ServerResponseItem responseItem, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(responseItem.RoomId) || string.IsNullOrWhiteSpace(responseItem.Message))
        {
            logger.LogWarning("[LOCO_EXEC] Invalid send_text payload. room={RoomId}", responseItem.RoomId);
            return;
        }

        await cliApiClient.SendMessageAsync(responseItem.RoomId, responseItem.Message, cancellationToken);
        logger.LogInformation("[LOCO_EXEC] Sent. room={RoomId}", responseItem.RoomId);
    }

    private static KakaoMessageData MapToKakaoMessageData(LocoRoomMessage message, string roomName) => new()
    {
        Source = KakaoMessageData.KakaoSource,
        RoomName = roomName,
        RoomId = message.RoomId,
        SenderHash = message.SenderId,
        SenderName = message.Nickname,
        Content = message.Text,
        LogId = message.Id,
        IsGroupChat = message.RoomType != "1:1",
        Time = message.Timestamp
    };

    private static ServerResponseItem CreateSingleResponseItem(ServerResponse response) => new()
    {
        Action = response.Action,
        RoomId = response.RoomId,
        Message = response.Message
    };

    private sealed record RoomSubscription(CancellationTokenSource CancellationTokenSource, Task StreamTask);
}
