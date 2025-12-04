namespace Travel;

/// <summary>
/// 旅行系统相关的数据模型
/// </summary>

/// <summary>
/// 旅行关卡留言信息
/// </summary>
public class StageMessageInfo
{
    /// <summary>
    /// 留言ID
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 关卡ID
    /// </summary>
    public int StageId { get; set; }

    /// <summary>
    /// 留言内容
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

