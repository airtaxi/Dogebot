using Dogebot.Server.Models;

namespace Dogebot.Server.Services;

public interface IBotSettingService
{
    Task<MessageDeliveryMode> GetMessageDeliveryModeAsync();

    Task SetMessageDeliveryModeAsync(MessageDeliveryMode messageDeliveryMode, string updatedBy);

    Task<bool> IsDengAiLongReplyEnabledAsync();

    Task SetDengAiLongReplyEnabledAsync(bool enabled, string updatedBy);
}
