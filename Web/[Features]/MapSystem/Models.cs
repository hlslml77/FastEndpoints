namespace MapSystem;

/// <summary>
/// 保存地图进度请求
/// </summary>
public class SaveMapProgressRequest
{

    /// <summary>
    /// 起点位置ID
    /// </summary>
    public int StartLocationId { get; set; }

    /// <summary>
    /// 终点位置ID
    /// </summary>
    public int EndLocationId { get; set; }

    /// <summary>
    /// 跑步距离（米）
    /// </summary>
    public decimal DistanceMeters { get; set; }
}

/// <summary>
/// 保存地图进度响应
/// </summary>
public class SaveMapProgressResponse
{
    /// <summary>
    /// 记录ID
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 起点位置ID
    /// </summary>
    public int StartLocationId { get; set; }

    /// <summary>
    /// 终点位置ID
    /// </summary>
    public int EndLocationId { get; set; }

    /// <summary>
    /// 跑步距离（米）
    /// </summary>
    public decimal DistanceMeters { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 访问地图点位请求
/// </summary>
public class VisitMapLocationRequest
{
    /// <summary>
    /// 地图点位ID
    /// </summary>
    public int LocationId { get; set; }

    /// <summary>
    /// 是否完成该点位（由客户端上报）
    /// </summary>
    public bool IsCompleted { get; set; }
}

/// <summary>
/// 奖励项
/// </summary>
public class RewardItem
{
    /// <summary>
    /// 物品ID
    /// </summary>
    public int ItemId { get; set; }

    /// <summary>
    /// 数量
    /// </summary>
    public int Amount { get; set; }
}

/// <summary>
/// 访问地图点位响应
/// </summary>
public class VisitMapLocationResponse
{
    /// <summary>
    /// 是否首次访问
    /// </summary>
    public bool IsFirstVisit { get; set; }

    /// <summary>
    /// 奖励列表
    /// </summary>
    public List<RewardItem>? Rewards { get; set; }

    /// <summary>
    /// 访问次数
    /// </summary>
    public int VisitCount { get; set; }

    /// <summary>
    /// 首次访问时间
    /// </summary>
    public DateTime FirstVisitTime { get; set; }

    /// <summary>
    /// 最后访问时间
    /// </summary>
    public DateTime LastVisitTime { get; set; }

    /// <summary>
    /// 地图点位信息
    /// </summary>
    public MapLocationInfo? LocationInfo { get; set; }
}

/// <summary>
/// 地图点位信息
/// </summary>
public class MapLocationInfo
{
    /// <summary>
    /// 位置ID
    /// </summary>
    public int LocationId { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 景点描述
    /// </summary>
    public string? ScenicSpot { get; set; }

    /// <summary>
    /// 层级
    /// </summary>
    public int Hierarchy { get; set; }
}



/// <summary>
/// 进度记录DTO
/// </summary>
public class PlayerProgressDto
{
    public int StartLocationId { get; set; }
    public int EndLocationId { get; set; }
    public decimal DistanceMeters { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 获取玩家地图状态响应
/// </summary>
public class GetPlayerMapStateResponse
{
    /// <summary>
    /// 已访问点位ID列表
    /// </summary>
    public List<int> VisitedLocationIds { get; set; } = new();

    /// <summary>
    /// 已完成点位ID列表
    /// </summary>
    public List<int> CompletedLocationIds { get; set; } = new();

    /// <summary>
    /// 所有路线进度记录
    /// </summary>
    public List<PlayerProgressDto> ProgressRecords { get; set; } = new();
}
