using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

/// <summary>
/// 玩家每天的运动汇总（按设备类型）。用于校验与回放，排行榜读取走聚合表即可。
/// 主键：UserId+Date+DeviceType
/// </summary>
[Table("player_sport_daily")]
public class PlayerSportDaily
{
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("date")]
    public DateOnly Date { get; set; }

    /// <summary>0=跑步,1=划船,2=单车,3=手环</summary>
    [Column("device_type")]
    public int DeviceType { get; set; }

    [Column("distance_meters")]
    public decimal DistanceMeters { get; set; }

    [Column("calories")]
    public int Calories { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 排行榜聚合表。每次运动完成时即刻累加，读取时直接 order by 即可。
/// 主键：PeriodType+PeriodId+DeviceType+UserId
/// </summary>
[Table("pve_rank_board")]
public class PveRankBoard
{
    /// <summary>1=周榜, 2=赛季榜</summary>
    [Column("period_type")]
    public int PeriodType { get; set; }

    /// <summary>周期ID：周榜=yyyyWW（ISO周），赛季=yyyy（默认以年作为赛季）或实现内的规则。</summary>
    [Column("period_id")]
    public int PeriodId { get; set; }

    /// <summary>0=跑步,1=划船,2=单车,3=手环</summary>
    [Column("device_type")]
    public int DeviceType { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("total_distance_meters")]
    public decimal TotalDistanceMeters { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// 奖励发放记录，避免重复发放。
/// </summary>
[Table("pve_rank_reward_grant")]
public class PveRankRewardGrant
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("period_type")]
    public int PeriodType { get; set; }

    [Column("period_id")]
    public int PeriodId { get; set; }

    [Column("device_type")]
    public int DeviceType { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("rank")]
    public int Rank { get; set; }

    /// <summary>JSON 保存实际发放的奖励快照</summary>
    [Column("reward_json")]
    public string RewardJson { get; set; } = string.Empty;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}

