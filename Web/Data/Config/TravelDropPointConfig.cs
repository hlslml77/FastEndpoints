namespace Web.Data.Config;

public class TravelDropPointConfig
{
    public int ID { get; set; }
    public int LevelID { get; set; }
    public int[]? Distance { get; set; }
    public int DropDistance { get; set; }
    public int[]? DropRandom { get; set; }
    public int[]? QuantitiesRandom { get; set; }
    public List<int[]>? FixReward { get; set; }
}

