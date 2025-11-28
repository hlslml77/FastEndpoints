using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

[Table("player_collection")]
public class PlayerCollection
{
    [Column("user_id")] public long UserId { get; set; }
    [Column("collection_id")] public int CollectionId { get; set; }
    [Column("obtained_at")] public DateTime ObtainedAt { get; set; }
}

[Table("player_collection_combo_claim")]
public class PlayerCollectionComboClaim
{
    [Column("user_id")] public long UserId { get; set; }
    [Column("combo_id")] public int ComboId { get; set; }
    [Column("claimed_at")] public DateTime ClaimedAt { get; set; }
}

