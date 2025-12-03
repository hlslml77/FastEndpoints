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
    public List<int>? Consumption { get; set; }

    /// <summary>
    /// 起点到下一个点的距离映射 [[endLocationId, distanceMeters], ...]
    /// </summary>
    public List<List<int>>? TheNextPointDistance { get; set; }


    /// <summary>
    /// 终点周边点位（解锁终点时可一并返回给客户端展示）
    /// </summary>
    public List<int>? SurroundingPoints { get; set; }

    /// <summary>
    /// 解锁该点位所需的最小距离（米），为null或0表示无需距离解锁
    /// </summary>
    public decimal? UnlockDistance { get; set; }

    /// <summary>
    /// 资源ID，0表示无倒计时
    /// </summary>
    public int Resources { get; set; } = 0;

    /// <summary>
    /// 刷新时间（小时）。当 Resources>0 时有效
    /// </summary>
    public int? RefreshTime { get; set; }
}

