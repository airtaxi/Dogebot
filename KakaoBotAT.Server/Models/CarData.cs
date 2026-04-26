namespace KakaoBotAT.Server.Models;

public class CarData
{
    public string Brand { get; set; } = string.Empty;
    public List<CarModel> Models { get; set; } = new();
}

public class CarModel
{
    public string Name { get; set; } = string.Empty;
    public List<string> Trims { get; set; } = new();
}
