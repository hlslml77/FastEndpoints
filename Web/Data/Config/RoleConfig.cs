namespace Web.Data.Config;

/// <summary>
/// 角色配置 - 对应 Role_Config 表
/// </summary>
public class RoleConfig
{
    /// <summary>
    /// 玩家初始上肢力量
    /// </summary>
    public int InitialUpperLimb { get; set; } = 10;

    /// <summary>
    /// 玩家初始下肢力量
    /// </summary>
    public int InitialLowerLimb { get; set; } = 10;

    /// <summary>
    /// 玩家初始核心力量
    /// </summary>
    public int InitialCore { get; set; } = 10;

    /// <summary>
    /// 玩家初始心肺力量
    /// </summary>
    public int InitialHeartLungs { get; set; } = 10;

    /// <summary>
    /// 上肢力量对速度的加成比例
    /// </summary>
    public decimal UpperLimbSpeedBonus { get; set; } = 0.02m;

    /// <summary>
    /// 下肢力量对速度的加成比例
    /// </summary>
    public decimal LowerLimbSpeedBonus { get; set; } = 0.02m;

    /// <summary>
    /// 核心力量对速度的加成比例
    /// </summary>
    public decimal CoreSpeedBonus { get; set; } = 0.02m;

    /// <summary>
    /// 心肺力量对速度的加成比例
    /// </summary>
    public decimal HeartLungsSpeedBonus { get; set; } = 0.02m;

    /// <summary>
    /// 每日可获得属性点上限
    /// </summary>
    public int DailyAttributePointsLimit { get; set; } = 10;

    /// <summary>
    /// 计算速度加成
    /// </summary>
    public decimal CalculateSpeedBonus(int upperLimb, int lowerLimb, int core, int heartLungs)
    {
        return (upperLimb * UpperLimbSpeedBonus) +
               (lowerLimb * LowerLimbSpeedBonus) +
               (core * CoreSpeedBonus) +
               (heartLungs * HeartLungsSpeedBonus);
    }
}

/// <summary>
/// 角色升级配置 - 对应 Role_Upgrade 表
/// </summary>
public class RoleUpgradeConfig
{
    /// <summary>
    /// 等级ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 升到下一级所需经验
    /// </summary>
    public int Experience { get; set; }

    /// <summary>
    /// 升级提升上肢力量值
    /// </summary>
    public int UpperLimb { get; set; }

    /// <summary>
    /// 升级提升下肢力量值
    /// </summary>
    public int LowerLimb { get; set; }

    /// <summary>
    /// 升级提升核心力量值
    /// </summary>
    public int Core { get; set; }

    /// <summary>
    /// 升级提升心肺力量值
    /// </summary>
    public int HeartLungs { get; set; }
}

/// <summary>
/// 运动配置 - 对应 Role_Sport 表
/// </summary>
public class RoleSportConfig
{
    /// <summary>
    /// ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 距离（公里）
    /// </summary>
    public decimal Distance { get; set; }

    /// <summary>
    /// 自行车提升下肢力量
    /// </summary>
    public int? BicycleLowerLimb { get; set; }

    /// <summary>
    /// 跑步提升心肺力量
    /// </summary>
    public int? RunHeartLungs { get; set; }

    /// <summary>
    /// 划船机提升上肢力量
    /// </summary>
    public int? RowingUpperLimb { get; set; }
}

/// <summary>
/// 经验配置 - 对应 Role_Experience 表
/// </summary>
public class RoleExperienceConfig
{
    /// <summary>
    /// ID
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 消耗热量值（焦耳）
    /// </summary>
    public int Joule { get; set; }

    /// <summary>
    /// 获得经验值
    /// </summary>
    public int Experience { get; set; }
}

