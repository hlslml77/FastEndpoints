namespace Web.Data.Config;

/// <summary>
/// 角色配置：全局配置项（目前只使用每日属性点上限）
/// </summary>
public class RoleConfig
{
    /// <summary>
    /// 每日可获得属性点上限（来自 Role_Config.json ID=1 的 Value1）
    /// </summary>
    public int DailyAttributePointsLimit { get; set; } = 10;
}

/// <summary>
/// 四个主属性的定义与每点带来的副属性加成（来自 Role_Attribute.json）
/// 注意：不再使用 AttributeID，按 Name 匹配 UpperLimb/LowerLimb/Core/HeartLungs
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
/// Distance（公里）以及不同设备类型下对四个主属性的加点分布
/// UpperLimb/LowerLimb/Core/HeartLungs 为二维数组 [ [DeviceType, Points], ... ]
/// 设备类型：0=跑步机，1=单车，2=划船机，3=手环
/// </summary>
public class RoleSportEntry
{
    public int ID { get; set; }
    public decimal Distance { get; set; }
    public List<List<int>>? UpperLimb { get; set; }
    public List<List<int>>? LowerLimb { get; set; }
    public List<List<int>>? Core { get; set; }
    public List<List<int>>? HeartLungs { get; set; }
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
/// </summary>
public class RoleExperienceConfig
{
    public int Id { get; set; }
    public int Joule { get; set; }
    public int Experience { get; set; }
}

