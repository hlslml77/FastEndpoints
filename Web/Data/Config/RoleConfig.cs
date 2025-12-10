namespace Web.Data.Config;

/// <summary>
/// 角色配置：全局配置项（目前只使用每日属性点上限）
/// </summary>
public class RoleConfig
{
    /// <summary>
    /// 每日可获得属性点上限（来自 Role_Config.json ID=1 的 Value1；若无文件则使用默认值）
    /// </summary>
    public int DailyAttributePointsLimit { get; set; } = 10;
}

/// <summary>
/// 四个主属性的定义与每点带来的副属性加成（来自 Role_Attribute.json）
/// 初始值 Initial 将由 Role_AttributeID.json 的 Init 字段合并（ID=1..4）。
/// </summary>
public class RoleAttributeDef
{
    public int Id { get; set; }
    public int Initial { get; set; }
    public string? Name { get; set; }

    // 每点主属性带来的副属性加成
    public decimal Attack { get; set; }
    public decimal HP { get; set; }
    public decimal Defense { get; set; }
    public decimal AttackSpeed { get; set; }
    public decimal Critical { get; set; }
    public decimal CriticalDamage { get; set; }
    public decimal Speed { get; set; }
    public decimal Efficiency { get; set; }
    public decimal Energy { get; set; }
}

/// <summary>
/// 角色升级配置（来自 Role_Upgrade.json）
/// 注意：JSON 字段为 Rank，与旧版本的 Id 不同
/// </summary>
public class RoleUpgradeConfig
{
    public int Rank { get; set; }
    public int Experience { get; set; }
    public int UpperLimb { get; set; }
    public int LowerLimb { get; set; }
    public int Core { get; set; }
    public int HeartLungs { get; set; }
}

/// <summary>
/// 运动配置（来自 Role_Sport.json）
/// Distance（公里）以及不同设备类型下对四个主属性的加点分布。
/// UpperLimb/LowerLimb/Core/HeartLungs 为长度为 4 的数组，表示四种设备的最终加点值：
/// 设备类型映射：0=跑步机；1=划船机；2=单车；3=手环。
/// 为兼容旧数据，加载时也支持旧格式 [[deviceType, points], ...] 并转换为长度为 4 的数组（按设备类型汇总）。
/// </summary>
public class RoleSportEntry
{
    public int ID { get; set; }
    public decimal Distance { get; set; }
    public List<int>? UpperLimb { get; set; }
    public List<int>? LowerLimb { get; set; }
    public List<int>? Core { get; set; }
    public List<int>? HeartLungs { get; set; }
}

/// <summary>
/// 计算得到的分配结果：四个主属性分别增加的点数
/// </summary>
public class SportDistributionResult
{
    public int UpperLimb { get; set; }
    public int LowerLimb { get; set; }
    public int Core { get; set; }
    public int HeartLungs { get; set; }
}

/// <summary>
/// 经验配置 - 对应 Role_Experience.json
/// 支持两种字段：
/// - Distance（米）：按运动距离给经验（当前使用）
/// - Joule（卡路里）：兼容旧字段
/// </summary>
public class RoleExperienceConfig
{
    public int Id { get; set; }
    public int Distance { get; set; } // meters
    public int Joule { get; set; }    // backward compat
    public int Experience { get; set; }
}

