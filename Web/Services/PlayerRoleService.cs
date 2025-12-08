using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Data.Entities;
using Serilog;

namespace Web.Services;

/// <summary>
/// 玩家角色服务
/// </summary>
public interface IPlayerRoleService
{
    Task<PlayerRole> GetOrCreatePlayerAsync(long userId);
    Task<PlayerRole> CompleteSportAsync(long userId, int deviceType, decimal distance, int calorie);

    // 即时计算副属性（不落库）
    SecondaryAttributes ComputeSecondary(PlayerRole player);
}

public class SecondaryAttributes
{
    public decimal Attack { get; set; }
    public decimal HP { get; set; }
    public decimal Defense { get; set; }
    public decimal AttackSpeed { get; set; }
    public decimal Critical { get; set; }
    public decimal CriticalDamage { get; set; }
    public decimal Speed { get; set; }
    public decimal Efficiency { get; set; }
    public decimal Energy { get; set; }
}

public class PlayerRoleService : IPlayerRoleService
{
    private readonly AppDbContext _dbContext;
    private readonly IRoleConfigService _configService;
    private readonly IInventoryService _inventoryService;
    private readonly IGeneralConfigService _generalConfigService;
    private readonly IPveRankService _pveRankService;

    public PlayerRoleService(
        AppDbContext dbContext,
        IRoleConfigService configService,
        IInventoryService inventoryService,
        IGeneralConfigService generalConfigService,
        IPveRankService pveRankService)
    {
        _dbContext = dbContext;
        _configService = configService;
        _inventoryService = inventoryService;
        _generalConfigService = generalConfigService;
        _pveRankService = pveRankService;
    }

    /// <summary>
    /// 获取或创建玩家角色
    /// </summary>
    public async Task<PlayerRole> GetOrCreatePlayerAsync(long userId)
    {
        var player = await _dbContext.PlayerRole.FindAsync(userId);

        if (player == null)
        {
            // 初始主属性来自 Role_Attribute.json（按 Name 匹配）
            var defs = _configService.GetAttributeDefs();
            int initUpper = defs.FirstOrDefault(d => string.Equals(d.Name, "UpperLimb", StringComparison.OrdinalIgnoreCase))?.Initial ?? 0;
            int initLower = defs.FirstOrDefault(d => string.Equals(d.Name, "LowerLimb", StringComparison.OrdinalIgnoreCase))?.Initial ?? 0;
            int initCore = defs.FirstOrDefault(d => string.Equals(d.Name, "Core", StringComparison.OrdinalIgnoreCase))?.Initial ?? 0;
            int initHeart = defs.FirstOrDefault(d => string.Equals(d.Name, "HeartLungs", StringComparison.OrdinalIgnoreCase))?.Initial ?? 0;

            player = new PlayerRole
            {
                UserId = userId,
                CurrentLevel = 1,
                CurrentExperience = 0,
                AttrUpperLimb = initUpper,
                AttrLowerLimb = initLower,
                AttrCore = initCore,
                AttrHeartLungs = initHeart,
                TodayAttributePoints = 0,
                LastUpdateTime = DateTime.UtcNow
            };

            _dbContext.PlayerRole.Add(player);


                // 设置玩家初始位置并默认解锁
                var initLoc = _generalConfigService.GetInitialLocationId();
                if (initLoc > 0)
                {
                    player.CurrentLocationId = initLoc;
                    _dbContext.PlayerUnlockedLocation.Add(new Web.Data.Entities.PlayerUnlockedLocation
                    {
                        UserId = userId,
                        LocationId = initLoc,
                        UnlockedTime = DateTime.UtcNow
                    });
                }

            // 按配置发放初始金币与体力道具
            var goldItemId = _generalConfigService.InitialGoldItemId;     // 默认1000
            var staminaItemId = _generalConfigService.InitialStaminaItemId; // 默认1002
            var goldAmount = Math.Max(0, _generalConfigService.GetInitialGoldAmount());
            var staminaAmount = Math.Max(0, _generalConfigService.GetInitialStaminaAmount());

            if (goldAmount > 0)
                await _inventoryService.GrantItemAsync(userId, goldItemId, goldAmount);
            if (staminaAmount > 0)
                await _inventoryService.GrantItemAsync(userId, staminaItemId, staminaAmount);

            await _dbContext.SaveChangesAsync();

            Log.Information("Created new player role for user {UserId}. init gold:{GoldId} x{GoldAmt}, stamina:{StaId} x{StaAmt}", userId, goldItemId, goldAmount, staminaItemId, staminaAmount);
        }

        return player;
    }

    /// <summary>
    /// 检查并升级（根据 Role_Upgrade.json 的 Rank 配表）
    /// </summary>
    private void CheckAndLevelUp(PlayerRole player)
    {
        while (true)
        {
            var nextLevelConfig = _configService.GetUpgradeConfig(player.CurrentLevel);
            if (nextLevelConfig == null)
            {
                // 已达到最高等级
                break;
            }

            if (player.CurrentExperience >= nextLevelConfig.Experience)
            {
                // 升级
                player.CurrentExperience -= nextLevelConfig.Experience;
                player.CurrentLevel++;

                // 增加主属性
                player.AttrUpperLimb += nextLevelConfig.UpperLimb;
                player.AttrLowerLimb += nextLevelConfig.LowerLimb;
                player.AttrCore += nextLevelConfig.Core;
                player.AttrHeartLungs += nextLevelConfig.HeartLungs;

                Log.Information(
                    "User {UserId} leveled up to {Level}! Attributes: Upper={Upper}, Lower={Lower}, Core={Core}, Heart={Heart}",
                    player.UserId, player.CurrentLevel,
                    player.AttrUpperLimb, player.AttrLowerLimb, player.AttrCore, player.AttrHeartLungs);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// 即时计算副属性，不落库
    /// </summary>
    public SecondaryAttributes ComputeSecondary(PlayerRole player)
    {
        var defs = _configService.GetAttributeDefs();

        decimal attack = 0, hp = 0, defense = 0, atkSpd = 0, critical = 0, critDmg = 0, speed = 0, eff = 0, energy = 0;
        foreach (var def in defs)
        {
            var name = def.Name?.ToLowerInvariant();
            var pts = name switch
            {
                "upperlimb" => player.AttrUpperLimb,
                "lowerlimb" => player.AttrLowerLimb,
                "core" => player.AttrCore,
                "heartlungs" => player.AttrHeartLungs,
                _ => 0
            };

            attack += pts * def.Attack;
            hp += pts * def.HP;
            defense += pts * def.Defense;
            atkSpd += pts * def.AttackSpeed;
            critical += pts * def.Critical;
            critDmg += pts * def.CriticalDamage;
            speed += pts * def.Speed;
            eff += pts * def.Efficiency;
            energy += pts * def.Energy;
        }

        return new SecondaryAttributes
        {
            Attack = attack,
            HP = hp,
            Defense = defense,
            AttackSpeed = atkSpd,
            Critical = critical,
            CriticalDamage = critDmg,
            Speed = speed,
            Efficiency = eff,
            Energy = energy
        };
    }

    /// <summary>
    /// 完成运动，统一处理属性和经验增长
    /// </summary>
    public async Task<PlayerRole> CompleteSportAsync(long userId, int deviceType, decimal distance, int calorie)
    {
        var player = await GetOrCreatePlayerAsync(userId);

        // 配置表、客户端 deviceType: 0=跑步, 1=划船, 2=单车, 3=手环
        // 距离单位从米转换为公里，以匹配配置表
        var distanceInKm = distance / 1000.0m;

        var dist = _configService.GetSportDistribution(deviceType, distanceInKm);

        if (dist == null)
        {
            throw new ArgumentException($"Invalid sport distribution: deviceType={deviceType} at {distance}m");
        }

        // 1. 根据分配表增加对应主属性，并累计今日属性点（按总和限制）
        var cfg = _configService.GetRoleConfig();
        var addedPoints = dist.UpperLimb + dist.LowerLimb + dist.Core + dist.HeartLungs;

        player.AttrUpperLimb += dist.UpperLimb;
        player.AttrLowerLimb += dist.LowerLimb;
        player.AttrCore += dist.Core;
        player.AttrHeartLungs += dist.HeartLungs;

        // 应用每日上限（简单起见，不跨天清零，后续可根据需求在每日刷新时清零）
        player.TodayAttributePoints += addedPoints;
        if (player.TodayAttributePoints > cfg.DailyAttributePointsLimit)
            player.TodayAttributePoints = cfg.DailyAttributePointsLimit;

        // 1.1 副属性改为即时计算，不再落库

        // 2. 根据消耗的热量增加经验值
        var experience = _configService.GetExperienceFromJoules(calorie);
        if (experience > 0)
        {
            player.CurrentExperience += experience;
            Log.Information(
                "User {UserId} gained {Experience} experience from {Calorie} calories.",
                userId, experience, calorie);

            // 3. 检查是否升级
            CheckAndLevelUp(player);
        }

        player.LastUpdateTime = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        // 推送到排行榜聚合（异步不阻塞）
        try { _ = _pveRankService.AddSportAsync(userId, deviceType, distance, DateTime.UtcNow); }
        catch { /* ignore */ }

        Log.Information(
            "User {UserId} completed sport with device {DeviceType} for {Distance}m. Current level: {Level}, Experience: {CurrentExp}",
            userId, deviceType, distance, player.CurrentLevel, player.CurrentExperience);

        return player;
    }


}

