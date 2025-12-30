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

    // NOTE: 最新 Equipment.json 中存在小数（如 0.0 / 5.0），因此这里必须用 double[]
    // 读取后在生成装备实例时再按需要转 int。
    public double[]? AttackRange { get; set; }
    public double[]? HPRange { get; set; }
    public double[]? DefenseRange { get; set; }
    public double[]? Critical { get; set; }
    public double[]? AttackSpeed { get; set; }
    public double[]? CriticalDamage { get; set; }
    public double[]? UpperLimbRange { get; set; }
    public double[]? LowerLimbRange { get; set; }
    public double[]? CoreRange { get; set; }
    public double[]? HeartLungsRange { get; set; }

    public int Random { get; set; }
}
public class EquipmentRandomConfig
{
    public int ID { get; set; }
    public int RandomGroup { get; set; }
    public double[]? AttackRange { get; set; }
    public double[]? HPRange { get; set; }
    public double[]? DefenseRange { get; set; }
    public double[]? AttackSpeedRange { get; set; }
    public double[]? CriticalRange { get; set; }
    public double[]? CriticalDamageRange { get; set; }
    public double[]? EfficiencyRange { get; set; }
    public double[]? EnergyRange { get; set; }
    public double[]? SpeedRange { get; set; }
}

public class EquipmentEntryConfig
{
    public int ID { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public string Description { get; set; } = string.Empty;
}

public readonly record struct IntRange(int Min, int Max);

