using System.ComponentModel.DataAnnotations;

namespace MapSystem.GetEnergyCapacity;

public class EnergyCapacityRequest
{
    /// <summary>
    /// 玩家ID（可选；不传时从 JWT 中解析）
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// APP服务认证令牌（将转发到 APP 的 http header: appToken）
    /// </summary>
    [Required(ErrorMessage = "APP认证令牌不能为空")]
    public string AppToken { get; set; } = string.Empty;
}

