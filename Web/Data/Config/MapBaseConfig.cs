namespace Web.Data.Config;

/// <summary>
/// 地图基础配置 - 对应 MapBase.json
/// </summary>
public class MapBaseConfig
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// 位置ID
    /// </summary>
    public int LocationID { get; set; }

    /// <summary>
    /// 线路类型
    /// </summary>
    public int LineType { get; set; }

    /// <summary>
    /// 地图ID
    /// </summary>
    public int MapID { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Des { get; set; }

    /// <summary>
    /// 区域ID
    /// </summary>
    public int AreaID { get; set; }

    /// <summary>
    /// 层级
    /// </summary>
    public int Hierarchy { get; set; }

    /// <summary>
    /// 范围
    /// </summary>
    public int Scope { get; set; }

    /// <summary>
    /// 前一个点
    /// </summary>
    public int ThePreviousPoint { get; set; }

    /// <summary>
    /// 下一个点距离
    /// </summary>
    public List<List<int>>? TheNextPointDistance { get; set; }

    /// <summary>
    /// 首次奖励 [[物品ID, 数量], ...]
    /// </summary>
    public List<List<int>>? FirstReward { get; set; }

    /// <summary>
    /// 固定奖励 [[物品ID, 数量], ...]
    /// </summary>
    public List<List<int>>? FixedReward { get; set; }

    /// <summary>
    /// 资源
    /// </summary>
    public int Resources { get; set; }

    /// <summary>
    /// 挑战类型
    /// </summary>
    public int ChallengeRype { get; set; }

    /// <summary>
    /// 景点描述
    /// </summary>
    public string? ScenicSpot { get; set; }

    /// <summary>
    /// 剧情
    /// </summary>
    public int Plot { get; set; }
}

