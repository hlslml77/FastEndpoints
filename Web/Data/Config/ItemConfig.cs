namespace Web.Data.Config;

public class ItemConfig
{
    public int ID { get; set; }
    public string? Name { get; set; }
    public int PropType { get; set; } // 1=item(可叠加), 2=equipment(单件)
    public int Quality { get; set; }
    public int Part { get; set; } // 装备部位（当 PropType=2 时有意义）
    public int StackingUpperLimit { get; set; }
}

public class EquipmentConfig
{
    public int ID { get; set; }          // 行ID（无业务含义）
    public int EquipID { get; set; }     // 对应 Item.json 里的 ID（且 PropType=2）
    public int Quality { get; set; }
    public int Part { get; set; }

    // 属性区间（字符串配置如 "3|20" 已在服务中解析成 IntRange）
    public string? AttackRange { get; set; }
    public string? HPRange { get; set; }
    public string? DefenseRange { get; set; }
    public string? Critical { get; set; }
    public string? AttackSpeed { get; set; }
    public string? CriticalDamage { get; set; }
    public string? UpperLimbRange { get; set; }
    public string? LowerLimbRange { get; set; }
    public string? CoreRange { get; set; }
    public string? HeartLungsRange { get; set; }
}

public readonly record struct IntRange(int Min, int Max);

