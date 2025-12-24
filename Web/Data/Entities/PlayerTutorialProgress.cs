using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

/// <summary>
/// 玩家新手引导进度表
/// </summary>
[Table("player_tutorial_progress")]
public class PlayerTutorialProgress
{
    /// <summary>
    /// 用户ID（主键）
    /// </summary>
    [Key]
    [Column("user_id")]
    public long UserId { get; set; }

    /// <summary>
    /// 当前步骤ID
    /// </summary>
    [Column("current_step_id")]
    public int CurrentStepId { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    [Column("last_updated")]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
