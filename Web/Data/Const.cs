namespace Web.Data.Config;

/// <summary>
/// 道具类型常量，替代代码中直接使用的魔法数字（1/2）。
/// </summary>
public static class ItemPropTypes
{
    /// <summary>
    /// 可叠加的普通道具（如金币等）
    /// </summary>
    public const int Stackable = 1;

    /// <summary>
    /// 装备（单件，带词条）
    /// </summary>
    public const int Equipment = 2;
}

/// <summary>
/// 装备部位常量（来自策划配置 Item.json 的 Part 含义）
/// </summary>
public static class EquipPart
{
    public const int Weapon = 1;
    public const int Gloves = 2;
    public const int UpperBody = 3;
    public const int Pants = 4;
}

/// <summary>
/// 品质常量（1/2/3）
/// </summary>
public static class ItemQuality
{
    public const int Common = 1;
    public const int Rare = 2;
    public const int Epic = 3;
}

/// <summary>
/// 设备类型（complete-sport）
/// 0=跑步机, 1=单车, 2=划船机, 3=手环
/// </summary>
public static class DeviceTypes
{
    public const int Treadmill = 0;
    public const int Bicycle = 1;
    public const int Rowing = 2;
    public const int Band = 3;
}

/// <summary>
/// Role_Config.json 中各配置项ID
/// </summary>
public static class RoleConfigIds
{
    /// <summary>
    /// 每日属性点上限所在行的 ID（策划表约定）
    /// </summary>
    public const int DailyAttributePointsRowId = 1;
}

