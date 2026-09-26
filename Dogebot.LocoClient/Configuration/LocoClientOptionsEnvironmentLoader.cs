using System.Globalization;

namespace Dogebot.LocoClient.Configuration;

/// <summary>
/// Builds <see cref="LocoClientOptions"/> from environment variables, falling back to defaults when a variable is not set.
/// </summary>
public static class LocoClientOptionsEnvironmentLoader
{
    public const string CliBaseUrlVariableName = "LOCO_CLI_BASE_URL";
    public const string ServerBaseUrlVariableName = "LOCO_SERVER_BASE_URL";
    public const string CommandPollIntervalSecondsVariableName = "LOCO_COMMAND_POLL_INTERVAL_SECONDS";
    public const string RoomRefreshIntervalSecondsVariableName = "LOCO_ROOM_REFRESH_INTERVAL_SECONDS";
    public const string WebSocketReconnectDelaySecondsVariableName = "LOCO_WEBSOCKET_RECONNECT_DELAY_SECONDS";

    public static LocoClientOptions Load()
    {
        var defaults = new LocoClientOptions();

        return new LocoClientOptions
        {
            CliBaseUrl = ReadString(CliBaseUrlVariableName) ?? defaults.CliBaseUrl,
            ServerBaseUrl = ReadString(ServerBaseUrlVariableName) ?? defaults.ServerBaseUrl,
            CommandPollIntervalSeconds = ReadInt32(CommandPollIntervalSecondsVariableName) ?? defaults.CommandPollIntervalSeconds,
            RoomRefreshIntervalSeconds = ReadInt32(RoomRefreshIntervalSecondsVariableName) ?? defaults.RoomRefreshIntervalSeconds,
            WebSocketReconnectDelaySeconds = ReadInt32(WebSocketReconnectDelaySecondsVariableName) ?? defaults.WebSocketReconnectDelaySeconds
        };
    }

    private static string? ReadString(string variableName) => Environment.GetEnvironmentVariable(variableName) is { Length: > 0 } value ? value : null;

    private static int? ReadInt32(string variableName)
    {
        var value = ReadString(variableName);
        if (value is null) return null;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)) return parsedValue;

        throw new InvalidOperationException($"Environment variable {variableName} must be an integer. Current value: {value}");
    }
}
