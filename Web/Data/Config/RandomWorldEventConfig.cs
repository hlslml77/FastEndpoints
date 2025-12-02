namespace Web.Data.Config;

/// <summary>
/// 随机事件配置条目 - 对应 WorldUiMap_RandomEvent.json
/// </summary>
public class RandomEventConfigEntry
{
    public int ID { get; set; }
    public int EventType { get; set; }
    public string? Dialogue { get; set; }
    public List<List<int>>? FixedReward { get; set; }
    public List<List<int>>? Consumption { get; set; }
    public int Probability { get; set; }
}

/// <summary>
/// 随机事件点位配置 - 对应 WorldUiMap_RandomPoint.json
/// </summary>
public class RandomPointConfigEntry
{
    public int ID { get; set; }
    public int PositioningPoint { get; set; }
    public string? UnlockMap { get; set; }
}

