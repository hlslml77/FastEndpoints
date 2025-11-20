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
    /// 速度加成
    /// </summary>
    public decimal SpeedBonus { get; set; }

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

