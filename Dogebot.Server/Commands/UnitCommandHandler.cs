using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

public class UnitCommandHandler(IUnitConversionService unitConversionService, ILogger<UnitCommandHandler> logger) : ICommandHandler
{
    private const string UnitCommand = "!단위";

    public string Command => UnitCommand;

    public bool CanHandle(string content) => TryGetQueryText(content, out _);

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            TryGetQueryText(data.Content, out var queryText);
            var message = await unitConversionService.CreateUnitConversionMessageAsync(queryText);

            logger.LogInformation("[UNIT] Unit conversion requested by {Sender} in room {RoomId}: {QueryText}", data.SenderName, data.RoomId, queryText);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "[UNIT] Error processing unit conversion command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "단위 변환 중 오류가 발생했습니다."
            };
        }
    }

    private static bool TryGetQueryText(string content, out string queryText)
    {
        queryText = string.Empty;
        var trimmedContent = content.Trim();
        if (trimmedContent.Equals(UnitCommand, StringComparison.OrdinalIgnoreCase)) return true;
        if (!trimmedContent.StartsWith(UnitCommand, StringComparison.OrdinalIgnoreCase)) return false;
        if (trimmedContent.Length <= UnitCommand.Length || !char.IsWhiteSpace(trimmedContent[UnitCommand.Length])) return false;

        queryText = trimmedContent[UnitCommand.Length..].Trim();
        return true;
    }
}
