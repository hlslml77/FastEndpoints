namespace Web.Data.Config;

public class ItemConfig
{
    public int ID { get; set; }
    // Name 在 JSON 中是本地化ID（数值），因此用 int? 以兼容当前数据格式
    public int? Name { get; set; }
    public int PropType { get; set; } // 1=item(可叠加), 2=equipment(单件)
    public int Quality { get; set; }
    public int Part { get; set; } // 装备部位（当 PropType=2 时有意义）
    public int StackingUpperLimit { get; set; }
}

public class EquipmentConfig
{
    /// <summary>
    /// 装备ID（与 Item.json 里的装备道具ID一致；历史上叫 equipID）
    /// </summary>
    public int ID { get; set; }

    public int Quality { get; set; }
    public int Part { get; set; }

    // 这些字段在最新 JSON 中为整型区间 [min,max]
    public int[]? AttackRange { get; set; }
    public int[]? HPRange { get; set; }
    public int[]? DefenseRange { get; set; }
    public int[]? CriticalRange { get; set; }
    public int[]? AttackSpeedRange { get; set; }
    public int[]? CriticalDamage { get; set; }
    public int[]? UpperLimbRange { get; set; }
    public int[]? LowerLimbRange { get; set; }
    public int[]? CoreRange { get; set; }
    public int[]? HeartLungsRange { get; set; }

    public int Random { get; set; }
}
public class EquipmentRandomConfig
{
    public int ID { get; set; }

    // 这些字段在最新 JSON 中为整型区间 [min,max]
    public int[]? AttackRange { get; set; }
    public int[]? HPRange { get; set; }
    public int[]? DefenseRange { get; set; }
    public int[]? AttackSpeedRange { get; set; }
    public int[]? CriticalRange { get; set; }
    public int[]? CriticalDamageRange { get; set; }
    public int[]? EfficiencyRange { get; set; }
    public int[]? EnergyRange { get; set; }
    public int[]? SpeedRange { get; set; }
}

public class EquipmentEntryConfig
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Description { get; set; } = string.Empty;
}

public readonly record struct IntRange(int Min, int Max);

