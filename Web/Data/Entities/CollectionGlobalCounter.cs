using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Web.Data.Entities;

[Table("collection_global_counter")]
public class CollectionGlobalCounter
{
    [Key]
    [Column("collection_id")]
    public int CollectionId { get; set; }

    [Column("total_obtained")]
    public int TotalObtained { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

