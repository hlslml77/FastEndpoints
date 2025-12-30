namespace Web.Data
{
    /// <summary>角色主属性</summary>
    public enum PlayerBuffType
    {
        UpperLimb  = 0,   // 上肢力量
        LowerLimb  = 1,   // 下肢力量
        CoreRange  = 2,   // 核心控制
        HeartLungs = 3,   // 心肺功能

        Max = 100        // 占位
    }

    /// <summary>装备加成属性</summary>
    public enum EquipBuffType
    {
        Attack         = 100, // 攻击
        Hp             = 101, // 生命
        Defense        = 102, // 防御
        Critical       = 103, // 暴击率
        AttackSpeed    = 104, // 攻击速度
        CriticalDamage = 105, // 暴击伤害
        Efficiency     = 106, // 产出效率
        Energy         = 107, // 能量储存上限
        Speed          = 108, // 平均速度

        Experience     = 200  // 经验获取效率
    }
}
