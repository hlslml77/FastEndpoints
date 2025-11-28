namespace Web.Data.Config;

public class CollectionItemCfg
{
    public int ID { get; set; }
    public string CollectionName { get; set; } = string.Empty;
    public int Weight { get; set; }
    public int Image { get; set; }
    /// <summary>
    /// [part, equipId] or null
    /// </summary>
    public int[]? ClothingParts { get; set; }
    /// <summary>
    /// 0 or null => unlimited; >0 => global cap
    /// </summary>
    public int? LimitedEditionCollectibles { get; set; }
    public string? Description { get; set; }
}

public class CollectionComboCfg
{
    public int ID { get; set; }
    public string Description { get; set; } = string.Empty;
    /// <summary>
    /// Required collection IDs; null => all collections required
    /// </summary>
    public int[]? MeetTheConditions { get; set; }
    /// <summary>
    /// Reward pairs [itemId, amount]
    /// </summary>
    public int[][] Reward { get; set; } = [];
}

