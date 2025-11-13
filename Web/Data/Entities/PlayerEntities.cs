using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

/// <summary>
/// 玩家角色成长数据表
/// </summary>
[Table("player_role_growth")]
public class PlayerRoleGrowth
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

