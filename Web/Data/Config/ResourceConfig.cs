namespace Web.Data.Config;

/// <summary>
/// 资源配置 - 对应 WorldUiMap_Resources.json
/// </summary>
public class ResourceConfig
{
    /// <summary>
    /// 配置ID
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// 资源ID（对应MapBaseConfig的Resources字段）
    /// </summary>
    public int Resources { get; set; }

    /// <summary>
    /// 关卡配置ID
    /// </summary>
    public int LevelConfig { get; set; }

    /// <summary>
    /// 刷新时间（小时），即倒计时时长
    /// </summary>
    public int RefreshTime { get; set; }
}

