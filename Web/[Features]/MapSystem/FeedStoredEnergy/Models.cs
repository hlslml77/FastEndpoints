using System.Collections.Generic;

namespace MapSystem.FeedStoredEnergy;

public class FeedEnergyRequest
{
    /// <summary>
    /// 玩家ID（可选；若不传则从 JWT 解析）
    /// </summary>
    public long? UserId { get; set; }

    /// <summary>
    /// 设备类型：0=跑步机,1=划船机,2=单车,3=手环/无设备
    /// </summary>
    public int DeviceType { get; set; }

    /// <summary>
    /// 本次上传的运动距离（米）
    /// </summary>
    public decimal DistanceMeters { get; set; }
}

public class FeedEnergyResponse
{
    /// <summary>
    /// 本次实际消耗的距离（米）。当能量槽已满或距离过多时，可能小于请求的 DistanceMeters。
    /// </summary>
    public decimal UsedDistanceMeters { get; set; }

    /// <summary>
    /// 玩家当前存储能量（米）
    /// </summary>
    public decimal StoredEnergyMeters { get; set; }
}

public class EnergyCapacityResponse
{
    /// <summary>
    /// 玩家距离能量槽上限还可存储的能量（米）
    /// </summary>
    public decimal RemainingEnergyMeters { get; set; }

    /// <summary>
    /// 四个设备类型分别还可灌输的最大距离（米）
    /// </summary>
    public List<DeviceDistanceInfo> DeviceDistances { get; set; } = new();
}

public class DeviceDistanceInfo
{
    public int DeviceType { get; set; }
    public decimal DistanceMeters { get; set; }
}

