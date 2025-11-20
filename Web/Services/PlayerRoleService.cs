using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Data.Entities;

namespace Web.Services;

/// <summary>
/// 玩家角色服务
/// </summary>
public interface IPlayerRoleService
{
    Task<PlayerRole> GetOrCreatePlayerAsync(long userId);
    Task<PlayerRole> CompleteSportAsync(long userId, int deviceType, decimal distance, int calorie);
}

public class PlayerRoleService : IPlayerRoleService
{
    private readonly AppDbContext _dbContext;
    private readonly IRoleConfigService _configService;
    private readonly ILogger<PlayerRoleService> _logger;

    public PlayerRoleService(
        AppDbContext dbContext,
        IRoleConfigService configService,
        ILogger<PlayerRoleService> logger)
    {
        _dbContext = dbContext;
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// 获取或创建玩家角色
    /// </summary>
    public async Task<PlayerRole> GetOrCreatePlayerAsync(long userId)
    {
        var player = await _dbContext.PlayerRole.FindAsync(userId);

        if (player == null)
        {
            var config = _configService.GetRoleConfig();
            player = new PlayerRole
            {
                UserId = userId,
                CurrentLevel = 1,
                CurrentExperience = 0,
                AttrUpperLimb = config.InitialUpperLimb,
                AttrLowerLimb = config.InitialLowerLimb,
                AttrCore = config.InitialCore,
                AttrHeartLungs = config.InitialHeartLungs,
                TodayAttributePoints = 0,
                LastUpdateTime = DateTime.UtcNow
            };

            _dbContext.PlayerRole.Add(player);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created new player role for user {UserId}", userId);
        }

        return player;
    }

    /// <summary>
    /// 检查并升级
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

                // 增加属性
                player.AttrUpperLimb += nextLevelConfig.UpperLimb;
                player.AttrLowerLimb += nextLevelConfig.LowerLimb;
                player.AttrCore += nextLevelConfig.Core;
                player.AttrHeartLungs += nextLevelConfig.HeartLungs;

                _logger.LogInformation(
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
    /// 完成运动，统一处理属性和经验增长
    /// </summary>
    public async Task<PlayerRole> CompleteSportAsync(long userId, int deviceType, decimal distance, int calorie)
    {
        var player = await GetOrCreatePlayerAsync(userId);
        var sportConfig = _configService.GetSportConfig(deviceType, distance);

        if (sportConfig == null)
        {
            throw new ArgumentException($"Invalid sport configuration: deviceType={deviceType} at {distance}km");
        }

        // 1. 根据运动类型增加对应属性
        switch (deviceType)
        {
            case 1: // Bicycle
                if (sportConfig.BicycleLowerLimb > 0)
                    player.AttrLowerLimb += sportConfig.BicycleLowerLimb.Value;
                break;
            case 2: // Run
                if (sportConfig.RunHeartLungs > 0)
                    player.AttrHeartLungs += sportConfig.RunHeartLungs.Value;
                break;
            case 3: // Rowing
                if (sportConfig.RowingUpperLimb > 0)
                    player.AttrUpperLimb += sportConfig.RowingUpperLimb.Value;
                break;
        }

        // 2. 根据消耗的热量增加经验值
        var experience = _configService.GetExperienceFromJoules(calorie);
        if (experience > 0)
        {
            player.CurrentExperience += experience;
            _logger.LogInformation(
                "User {UserId} gained {Experience} experience from {Calorie} calories.",
                userId, experience, calorie);

            // 3. 检查是否升级
            CheckAndLevelUp(player);
        }

        player.LastUpdateTime = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "User {UserId} completed sport with device {DeviceType} for {Distance}km. Current level: {Level}, Experience: {CurrentExp}",
            userId, deviceType, distance, player.CurrentLevel, player.CurrentExperience);

        return player;
    }


}

