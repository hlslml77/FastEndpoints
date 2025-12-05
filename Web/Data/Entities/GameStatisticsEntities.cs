using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

/// <summary>
/// 每日游戏统计数据表
/// </summary>
[Table("daily_game_statistics")]
public class DailyGameStatistics
{
    /// <summary>
    /// 自增主键
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// 统计日期（UTC，yyyy-MM-dd）
    /// </summary>
    [Column("statistics_date")]
    public DateOnly StatisticsDate { get; set; }

    /// <summary>
    /// 当天新注册玩家数
    /// </summary>
    [Column("new_registrations")]
    public int NewRegistrations { get; set; } = 0;

    /// <summary>
    /// 当天活跃玩家数（至少登录一次）
    /// </summary>
    [Column("active_players")]
    public int ActivePlayers { get; set; } = 0;

    /// <summary>
    /// 最大在线人数
    /// </summary>
    [Column("max_online_players")]
    public int MaxOnlinePlayers { get; set; } = 0;

    /// <summary>
    /// 平均在线人数
    /// </summary>
    [Column("avg_online_players")]
    public decimal AvgOnlinePlayers { get; set; } = 0;

    /// <summary>
    /// 总玩家数（累计）
    /// </summary>
    [Column("total_players")]
    public int TotalPlayers { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 在线人数实时统计表
/// </summary>
[Table("online_players_snapshot")]
public class OnlinePlayersSnapshot
{
    /// <summary>
    /// 自增主键
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// 统计日期（UTC，yyyy-MM-dd）
    /// </summary>
    [Column("statistics_date")]
    public DateOnly StatisticsDate { get; set; }

    /// <summary>
    /// 统计时间（小时，0-23）
    /// </summary>
    [Column("hour")]
    public int Hour { get; set; }

    /// <summary>
    /// 该小时的在线人数
    /// </summary>
    [Column("online_count")]
    public int OnlineCount { get; set; }

    /// <summary>
    /// 记录时间
    /// </summary>
    [Column("recorded_at")]
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 玩家活动统计表
/// </summary>
[Table("player_activity_statistics")]
public class PlayerActivityStatistics
{
    /// <summary>
    /// 自增主键
    /// </summary>
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>
    /// 统计日期（UTC，yyyy-MM-dd）
    /// </summary>
    [Column("statistics_date")]
    public DateOnly StatisticsDate { get; set; }

    /// <summary>
    /// 完成的地图点位总数
    /// </summary>
    [Column("total_locations_completed")]
    public int TotalLocationsCompleted { get; set; } = 0;

    /// <summary>
    /// 完成的旅行事件总数
    /// </summary>
    [Column("total_events_completed")]
    public int TotalEventsCompleted { get; set; } = 0;

    /// <summary>
    /// 总跑步距离（米）
    /// </summary>
    [Column("total_distance_meters")]
    public decimal TotalDistanceMeters { get; set; } = 0;

    /// <summary>
    /// 平均玩家等级
    /// </summary>
    [Column("avg_player_level")]
    public decimal AvgPlayerLevel { get; set; } = 0;

    /// <summary>
    /// 创建时间
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

