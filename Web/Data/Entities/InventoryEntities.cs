using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

[Table("player_item")]
public class PlayerItem
{
    [Key, Column("user_id", Order = 0)]
    public long UserId { get; set; }

    [Key, Column("item_id", Order = 1)]
    public int ItemId { get; set; }

    [Column("amount")]
    public long Amount { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

}

[Table("player_equipment_item")]
public class PlayerEquipmentItem
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("user_id")]
    public long UserId { get; set; }

    [Column("equip_id")]
    public int EquipId { get; set; }

    [Column("quality")]
    public int Quality { get; set; }

    [Column("part")]
    public int Part { get; set; }

    // Rolled stats (nullable)
    [Column("attack")]
    public int? Attack { get; set; }

    [Column("hp")]
    public int? HP { get; set; }

    [Column("defense")]
    public int? Defense { get; set; }

    [Column("critical")]
    public int? Critical { get; set; }

    [Column("attack_speed")]
    public int? AttackSpeed { get; set; }

    [Column("critical_damage")]
    public int? CriticalDamage { get; set; }

    [Column("upper_limb")]
    public int? UpperLimb { get; set; }

    [Column("lower_limb")]
    public int? LowerLimb { get; set; }

    [Column("core")]
    public int? Core { get; set; }

    [Column("heart_lungs")]
    public int? HeartLungs { get; set; }

    [Column("is_equipped")]
    public bool IsEquipped { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

