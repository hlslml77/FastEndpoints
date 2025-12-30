namespace RoleApi;

/// <summary>
/// 玩家角色信息响应
/// </summary>
public class PlayerRoleResponse
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 当前等级
    /// </summary>
    public int CurrentLevel { get; set; }

    /// <summary>
    /// 当前经验值
    /// </summary>
    public int CurrentExperience { get; set; }

    /// <summary>
    /// 升到下一级所需经验
    /// </summary>
    public int ExperienceToNextLevel { get; set; }

    /// <summary>
    /// 上肢力量
    /// </summary>
    public int UpperLimb { get; set; }

    /// <summary>
    /// 下肢力量
    /// </summary>
    public int LowerLimb { get; set; }

    /// <summary>
    /// 核心力量
    /// </summary>
    public int Core { get; set; }

    /// <summary>
    /// 心肺力量
    /// </summary>
    public int HeartLungs { get; set; }

    /// <summary>
    /// 今日已使用属性点
    /// </summary>
    public int TodayAttributePoints { get; set; }

    /// <summary>
    /// 今日可用属性点
    /// </summary>
    public int AvailableAttributePoints { get; set; }

    /// <summary>
    /// 速度加成（等同于 SecSpeed）
    /// </summary>
    public decimal SpeedBonus { get; set; }

    // 九项副属性（即时计算，不落库）
    public decimal SecAttack { get; set; }
    public decimal SecHP { get; set; }
    public decimal SecDefense { get; set; }
    public decimal SecAttackSpeed { get; set; }
    public decimal SecCritical { get; set; }
    public decimal SecCriticalDamage { get; set; }
    public decimal SecSpeed { get; set; }
    public decimal SecEfficiency { get; set; }
    public decimal SecEnergy { get; set; }

    /// <summary>
    /// 统一属性数组，见 PlayerAttributeType 枚举说明
    /// </summary>
    public List<Web.Data.PlayerAttributeType>? Attributes { get; set; }

    /// <summary>
    /// 最后更新时间
    /// </summary>
    public DateTime LastUpdateTime { get; set; }
}

/// <summary>
/// 完成运动请求（从JWT解析用户ID）
/// </summary>
public class CompleteSportRequest
{
    /// <summary>
    /// 设备类型 (例如: 1=Bicycle, 2=Run, 3=Rowing)
    /// </summary>
    public int DeviceType { get; set; }

    /// <summary>
    /// 距离（公里）
    /// </summary>
    public decimal Distance { get; set; }

    /// <summary>
    /// 消耗的卡路里
    /// </summary>
    public int Calorie { get; set; }
}

