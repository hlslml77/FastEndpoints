using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

[Table("player_role_growth")]
public class PlayerRoleGrowth
{
    [Key]
    [Column("user_id")]
    public long UserId { get; set; }

    [Column("current_level")]
    public int CurrentLevel { get; set; } = 1;

    [Column("current_experience")]
    public int CurrentExperience { get; set; } = 0;

    [Column("attr_upper_limb")]
    public int AttrUpperLimb { get; set; } = 10;

    [Column("attr_lower_limb")]
    public int AttrLowerLimb { get; set; } = 10;

    [Column("attr_core")]
    public int AttrCore { get; set; } = 10;

    [Column("attr_heart_lungs")]
    public int AttrHeartLungs { get; set; } = 10;

    [Column("today_attribute_points")]
    public int TodayAttributePoints { get; set; } = 0;

    [Column("last_update_time")]
    public DateTime LastUpdateTime { get; set; }
}

