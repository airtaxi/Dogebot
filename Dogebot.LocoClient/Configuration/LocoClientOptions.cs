namespace Dogebot.LocoClient.Configuration;

/// <summary>
/// Environment variable based configuration for the kakao-cli bridge client.
/// </summary>
public class LocoClientOptions
{
    /// <summary>
    /// Base URL of the kakao-cli API mode server. Set with LOCO_CLI_BASE_URL.
    /// </summary>
    public string CliBaseUrl { get; set; } = "http://127.0.0.1:8880";

    /// <summary>
    /// Base URL of the Dogebot.Server /api/kakao endpoints. Set with LOCO_SERVER_BASE_URL.
    /// </summary>
    public string ServerBaseUrl { get; set; } = "https://your-server-url.com/api/kakao";

    /// <summary>
    /// Interval in seconds for polling /command for queued deliveries. Set with LOCO_COMMAND_POLL_INTERVAL_SECONDS.
    /// </summary>
    public int CommandPollIntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Interval in seconds for refreshing the chat room list. New rooms are subscribed in this cycle. Set with LOCO_ROOM_REFRESH_INTERVAL_SECONDS.
    /// </summary>
    public int RoomRefreshIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Delay in seconds before reconnecting a room WebSocket that was closed. Set with LOCO_WEBSOCKET_RECONNECT_DELAY_SECONDS.
    /// </summary>
    public int WebSocketReconnectDelaySeconds { get; set; } = 5;
}
