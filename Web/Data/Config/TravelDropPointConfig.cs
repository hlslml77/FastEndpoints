namespace Web.Data.Config;

public class TravelDropPointConfig
{
    public int ID { get; set; }
    public int LevelID { get; set; }
    public int[]? Distance { get; set; }
    public int DropDistance { get; set; }
    public int[]? DropRandom { get; set; }
    public int[]? QuantitiesRandom { get; set; }
    // FixReward 在 JSON 中是形如 [itemId, amount] 的一维数组
    public int[]? FixReward { get; set; }
}

