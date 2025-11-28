namespace Web.Data.Config;

/// <summary>
/// 地图基础配置 - 对应 WorldUiMap_MapBase.json
/// </summary>
public class MapBaseConfig
{
    public int LocationID { get; set; }
    public int Des { get; set; }
    public int Hierarchy { get; set; }
    public int ScenicSpot { get; set; }
    public List<List<int>>? FirstReward { get; set; }
    public List<List<int>>? FixedReward { get; set; }
}

