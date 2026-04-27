using Dogebot.Commons;
using Dogebot.Server.Services;

namespace Dogebot.Server.Commands;

/// <summary>
/// Handles the !핫딜 command to show a random hot deal from arca.live/b/hotdeal.
/// </summary>
public class HotDealCommandHandler(
    IHotDealService hotDealService,
    ILogger<HotDealCommandHandler> logger) : ICommandHandler
{
    public string Command => "!핫딜";

    public bool CanHandle(string content)
    {
        return content.Trim().Equals(Command, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ServerResponse> HandleAsync(KakaoMessageData data)
    {
        try
        {
            var deal = await hotDealService.GetRandomHotDealAsync();

            if (deal == null)
            {
                return new ServerResponse
                {
                    Action = "send_text",
                    RoomId = data.RoomId,
                    Message = "❌ 핫딜 정보를 가져올 수 없습니다.\n잠시 후 다시 시도해주세요."
                };
            }

            var priceInfo = string.IsNullOrEmpty(deal.Price) ? "가격 정보 없음" : deal.Price;
            var shippingInfo = string.IsNullOrEmpty(deal.ShippingCost) ? "배송비 정보 없음" : deal.ShippingCost;
            var mallInfo = string.IsNullOrEmpty(deal.Mall) ? "" : $"🏪 판매처: {deal.Mall}\n";

            var lastCacheTime = hotDealService.GetLastCacheTime();
            var cacheInfo = lastCacheTime.HasValue
                ? $"마지막 갱신: {TimeZoneInfo.ConvertTimeFromUtc(lastCacheTime.Value, TimeZoneInfo.FindSystemTimeZoneById("Korea Standard Time")):yyyy-MM-dd HH:mm:ss}"
                : "첫 조회";

            var message = $"🔥 오늘의 핫딜!\n\n" +
                         $"📦 {deal.Title}\n\n" +
                         $"💰 가격: {priceInfo}\n" +
                         $"🚚 배송비: {shippingInfo}\n" +
                         mallInfo +
                         $"\n🔗 {deal.Link}\n\n" +
                         $"ℹ️ 핫딜 목록은 3시간마다 갱신됩니다.\n" +
                         $"📅 {cacheInfo}";

            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("[HOTDEAL] Recommended deal '{Title}' to {Sender} in room {RoomId}",
                    deal.Title, data.SenderName, data.RoomId);

            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = message
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[HOTDEAL] Error processing hot deal command");
            return new ServerResponse
            {
                Action = "send_text",
                RoomId = data.RoomId,
                Message = "핫딜 조회 중 오류가 발생했습니다."
            };
        }
    }
}

