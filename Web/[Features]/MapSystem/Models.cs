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

    /// <summary>
    /// 本次解锁的点位ID列表（若无解锁则为空）。当解锁终点时包含终点；若终点配置了 SurroundingPoints，则一并包含其周边点位。
    /// </summary>
    public List<int> UnlockedLocationIds { get; set; } = new();

    /// <summary>
    /// 玩家当前存储能量总值（米）
    /// </summary>
    public decimal StoredEnergyMeters { get; set; }

    /// <summary>
    /// 本次下发的奖励列表（对应 FirstReward），若无奖励则为空数组
    /// </summary>
    public List<RewardItem> Rewards { get; set; } = new();
}


/// <summary>
/// 使用存储能量解锁终点请求
/// </summary>
public class UnlockWithEnergyRequest
{
    /// <summary>
    /// 起点位置ID
    /// </summary>
    public int StartLocationId { get; set; }

    /// <summary>
    /// 终点位置ID
    /// </summary>
    public int EndLocationId { get; set; }
}

/// <summary>
/// 使用存储能量解锁终点响应
/// </summary>
public class UnlockWithEnergyResponse
{
    /// <summary>
    /// 是否已解锁
    /// </summary>
    public bool IsUnlocked { get; set; }

    /// <summary>
    /// 本次消耗的能量（米）
    /// </summary>
    public decimal UsedEnergyMeters { get; set; }

    /// <summary>
    /// 玩家当前存储能量（米）
    /// </summary>
    public decimal StoredEnergyMeters { get; set; }

    /// <summary>
    /// 本次解锁的点位ID列表（若无解锁则为空）。当解锁终点时包含终点；若终点配置了 SurroundingPoints，则一并包含其周边点位。
    /// </summary>
    public List<int> UnlockedLocationIds { get; set; } = new();

    /// <summary>
    /// 本次下发的奖励列表（对应 FirstReward），若无奖励则为空数组
    /// </summary>
    public List<RewardItem> Rewards { get; set; } = new();
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

    /// <summary>
    /// 是否需要消耗（由客户端决定），为 true 时且配置存在 Consumption 才会扣除道具
    /// </summary>
    public bool NeedConsume { get; set; }
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

    /// <summary>
    /// 可选：剩余数量（仅对消耗项有效；奖励项为空）
    /// </summary>
    public int? Remaining { get; set; }
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
    /// 本次是否消耗了道具（当 isCompleted=false 且配置有 Consumption 时为 true）
    /// </summary>
    public bool DidConsumeItem { get; set; }

    /// <summary>
    /// 本次消耗的物品列表（每项：itemId, amount）。若无消耗则为 null。
    /// </summary>
    public List<RewardItem>? ConsumedItems { get; set; }

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
    public int Description { get; set; }

    /// <summary>
    /// 景点描述
    /// </summary>
    public int ScenicSpot { get; set; }

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
/// 每日随机事件 DTO
/// </summary>
public class DailyRandomEventDto
{
    public int LocationId { get; set; }
    public int EventId { get; set; }
    public int EventType { get; set; }
    public string? Dialogue { get; set; }
    public bool IsCompleted { get; set; }
}

/// <summary>
/// 完成每日随机事件请求
/// </summary>
public class CompleteRandomEventRequest
{
    /// <summary>
    /// 事件所在点位ID（PositioningPoint）
    /// </summary>
    public int LocationId { get; set; }

    /// <summary>
    /// 可选：事件ID（用于校验与客户端一致）
    /// </summary>
    public int? EventId { get; set; }
}

public class CompleteRandomEventResponse
{
    public bool Success { get; set; }
    public List<RewardItem>? Rewards { get; set; }
}


/// <summary>
/// 获取玩家地图状态响应
/// </summary>
public class GetPlayerMapStateResponse
{
    /// <summary>
    /// 已解锁点位ID列表（通过save-progress接口当距离超过配置时解锁）
    /// </summary>
    public List<int> UnlockedLocationIds { get; set; } = new();

    /// <summary>
    /// 已完成点位ID列表
    /// </summary>
    public List<int> CompletedLocationIds { get; set; } = new();

    /// <summary>
    /// 所有路线进度记录
    /// </summary>
    public List<PlayerProgressDto> ProgressRecords { get; set; } = new();

    /// <summary>
    /// 玩家当前存储能量总值（米）
    /// </summary>
    public decimal StoredEnergyMeters { get; set; }

    /// <summary>
    /// 今日随机事件列表
    /// </summary>
    public List<DailyRandomEventDto> DailyRandomEvents { get; set; } = new();

    /// <summary>
    /// 玩家当前所在点位ID（根据进度/访问设置）
    /// </summary>
    public int? CurrentLocationId { get; set; }
}

/// <summary>
/// 查询某点位当前人数 请求
/// </summary>
public class GetLocationPeopleCountRequest
{
    public int LocationId { get; set; }
}

/// <summary>
/// 查询某点位当前人数 响应
/// </summary>
public class GetLocationPeopleCountResponse
{
    public int PeopleCount { get; set; }

    /// <summary>
    /// 玩家的下次挑战时间（当该点位有资源倒计时时返回，无倒计时返回 DateTime.MinValue）
    /// </summary>
    public DateTime NextChallengeTime { get; set; }
}

