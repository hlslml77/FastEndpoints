

namespace MapSystem.GetEnergyCapacity;

public class EnergyCapacityRequest
{
    /// <summary>
    /// 玩家ID（可选；不传时从 JWT 中解析）
    /// </summary>
    public long? UserId { get; set; }
}

