namespace KakaoBotAT.Server.Services;

public interface IHotDealService
{
    Task<HotDealItem?> GetRandomHotDealAsync();
}

public class HotDealItem
{
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = string.Empty;
    public string ShippingCost { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Mall { get; set; } = string.Empty;
}
