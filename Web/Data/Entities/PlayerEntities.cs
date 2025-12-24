using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

/// <summary>
/// 玩家角色数据表
/// </summary>
[Table("player_role")]
public class PlayerRole
{
    /// <summary>
    /// 用户ID（主键）
    /// </summary>
    [Key]
    [Column("user_id")]
    public long UserId { get; set; }

    /// <summary>
    /// 当前等级
    /// </summary>
    [Column("current_level")]
    public int CurrentLevel { get; set; } = 1;

    /// <summary>
    /// 当前经验值
    /// </summary>
    [Column("current_experience")]
    public int CurrentExperience { get; set; } = 0;

    /// <summary>
    /// 上肢属性值
    /// </summary>
    [Column("attr_upper_limb")]
    public int AttrUpperLimb { get; set; } = 10;

    /// <summary>
    /// 下肢属性值
    /// </summary>
    [Column("attr_lower_limb")]
    public int AttrLowerLimb { get; set; } = 10;

    /// <summary>
    /// 核心属性值
    /// </summary>
    [Column("attr_core")]
    public int AttrCore { get; set; } = 10;

    /// <summary>
    /// 心肺属性值
    /// </summary>
    [Column("attr_heart_lungs")]
    public int AttrHeartLungs { get; set; } = 10;

    /// <summary>
    /// 今日获得的属性点
    /// </summary>
    [Column("today_attribute_points")]
    public int TodayAttributePoints { get; set; } = 0;

    /// <summary>
    /// 存储的能量（米），用于地图解锁；最大值：10000
    /// </summary>
    [Column("stored_energy_meters")]
    public decimal StoredEnergyMeters { get; set; } = 0;

    /// <summary>
    /// 玩家当前所在地图点位（大地图）
    /// </summary>
    [Column("current_location_id")]
    public int? CurrentLocationId { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [Column("last_update_time")]
    public DateTime LastUpdateTime { get; set; }
}

/// <summary>
/// 玩家地图进度记录表
/// </summary>
[Table("player_map_progress")]
public class PlayerMapProgress
{
    /// <summary>
    /// 自增主键
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    [Column("user_id")]
    public long UserId { get; set; }

    /// <summary>
    /// 起点位置ID
    /// </summary>
    [Column("start_location_id")]
    public int StartLocationId { get; set; }

    /// <summary>
    /// 终点位置ID
    /// </summary>
    [Column("end_location_id")]
    public int EndLocationId { get; set; }

    /// <summary>
    /// 跑步距离（米）
    /// </summary>
    [Column("distance_meters")]
    public decimal DistanceMeters { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 玩家地图点位访问记录表
/// </summary>
[Table("player_map_location_visit")]
public class PlayerMapLocationVisit
{
    /// <summary>
    /// 用户ID（复合主键之一）
    /// </summary>
    [Key]
    [Column("user_id", Order = 0)]
    public long UserId { get; set; }

    /// <summary>
    /// 地图点位ID（复合主键之一）
    /// </summary>
    [Key]
    [Column("location_id", Order = 1)]
    public int LocationId { get; set; }

    /// <summary>
    /// 首次访问时间
    /// </summary>
    [Column("first_visit_time")]
    public DateTime FirstVisitTime { get; set; }

    /// <summary>
    /// 访问次数
    /// </summary>
    [Column("visit_count")]
    public int VisitCount { get; set; }

    /// <summary>
    /// 最后访问时间
    /// </summary>
    [Column("last_visit_time")]
    public DateTime LastVisitTime { get; set; }
}



/// <summary>
/// 玩家已完成点位表
/// </summary>
[Table("player_completed_location")]
public class PlayerCompletedLocation
{
    /// <summary>
    /// 用户ID（复合主键之一）
    /// </summary>
    [Key]
    [Column("user_id", Order = 0)]
    public long UserId { get; set; }

    /// <summary>
    /// 地图点位ID（复合主键之一）
    /// </summary>
    [Key]
    [Column("location_id", Order = 1)]
    public int LocationId { get; set; }

    /// <summary>
    /// 完成时间
    /// </summary>
    [Column("completed_time")]
    public DateTime CompletedTime { get; set; }

    /// <summary>
    /// 下次可挑战时间（当该点位有资源倒计时时设置）
    /// </summary>
    [Column("next_challenge_time")]
    public DateTime? NextChallengeTime { get; set; }
}

/// <summary>
/// 玩家已解锁点位表
/// </summary>
[Table("player_unlocked_location")]
public class PlayerUnlockedLocation
{
    /// <summary>
    /// 用户ID（复合主键之一）
    /// </summary>
    [Key]
    [Column("user_id", Order = 0)]
    public long UserId { get; set; }

    /// <summary>
    /// 地图点位ID（复合主键之一）
    /// </summary>
    [Key]
    [Column("location_id", Order = 1)]
    public int LocationId { get; set; }

    /// <summary>
    /// 解锁时间
    /// </summary>
    [Column("unlocked_time")]
    public DateTime UnlockedTime { get; set; }
}


/// <summary>
/// 玩家每日随机事件
/// </summary>
[Table("player_daily_random_event")]
public class PlayerDailyRandomEvent
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    /// <summary>
    /// 日期（UTC，yyyy-MM-dd）
    /// </summary>
    [Column("date")]
    public DateOnly Date { get; set; }

    /// <summary>
    /// 所在点位（PositioningPoint）
    /// </summary>
    [Column("location_id")]
    public int LocationId { get; set; }

    /// <summary>
    /// 事件配置ID（WorldUiMap_RandomEvent.json 的 ID）
    /// </summary>
    [Column("event_id")]
    public int EventId { get; set; }

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// 地图点位人数统计表
/// </summary>
[Table("location_people_count")]
public class LocationPeopleCount
{
    /// <summary>
    /// 地图点位ID（主键）
    /// </summary>
    [Key]
    [Column("location_id")]
    public int LocationId { get; set; }

    /// <summary>
    /// 当前在该点位的玩家人数
    /// </summary>
    [Column("people_count")]
    public int PeopleCount { get; set; } = 0;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [Column("last_update_time")]
    public DateTime LastUpdateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 旅行关卡留言表
/// </summary>
[Table("travel_stage_message")]
public class TravelStageMessage
{
    /// <summary>
    /// 自增主键
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// 用户ID
    /// </summary>
    [Column("user_id")]
    public long UserId { get; set; }

    /// <summary>
    /// 关卡ID
    /// </summary>
    [Column("stage_id")]
    public int StageId { get; set; }

    /// <summary>
    /// 节点ID
    /// </summary>
    [Column("node_id")]
    public int NodeId { get; set; }


    /// <summary>
    /// 留言内容
    /// </summary>
    [Column("message_content")]
    [StringLength(500)]
    public string MessageContent { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}


/// <summary>
/// 玩家关注关系表
/// </summary>
[Table("player_follow")]
public class PlayerFollow
{
    /// <summary>
    /// 关注者用户ID（复合主键之一）
    /// </summary>
    [Key]
    [Column("follower_id", Order = 0)]
    public long FollowerId { get; set; }

    /// <summary>
    /// 被关注者用户ID（复合主键之一）
    /// </summary>
    [Key]
    [Column("target_user_id", Order = 1)]
    public long TargetUserId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 玩家偶遇计数表（两人组合）
/// </summary>
[Table("player_encounter")]
public class PlayerEncounter
{
    /// <summary>
    /// 自增主键
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// 用户A ID（较小）
    /// </summary>
    [Column("user_a_id")]
    public long UserAId { get; set; }

    /// <summary>
    /// 用户B ID（较大）
    /// </summary>
    [Column("user_b_id")]
    public long UserBId { get; set; }

    /// <summary>
    /// 偶遇次数
    /// </summary>
    [Column("encounter_count")]
    public int EncounterCount { get; set; } = 0;

    /// <summary>
    /// 最近更新时间
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
