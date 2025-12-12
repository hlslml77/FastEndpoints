namespace Web.Data.Config;

public class MonsterConfig
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public int? DisplayName { get; set; }
    public int Type { get; set; }
    public int Hp { get; set; }
    public List<List<int>>? Reward { get; set; }
    public string? Model { get; set; }
    public string? DestroyAni { get; set; }
    public string? BehitAni { get; set; }
}

