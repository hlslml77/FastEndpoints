using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Data.Config;
using Web.Data.Entities;
using Serilog;

namespace Web.Services;

/// <summary>
/// 地图服务接口
/// </summary>
public interface IMapService
{
    /// <summary>
    /// 保存地图进度，返回进度记录、是否解锁以及最新存储能量（米）
    /// </summary>
    Task<(PlayerMapProgress Progress, bool IsUnlock, decimal StoredEnergyMeters)> SaveMapProgressAsync(long userId, int startLocationId, int endLocationId, decimal distanceMeters);

    /// <summary>
    /// 访问地图点位，返回是否首次访问和奖励信息以及是否消耗了道具
    /// </summary>
    Task<MapLocationVisitResult> VisitMapLocationAsync(long userId, int locationId, bool isCompleted);

    /// <summary>
    /// 使用存储能量解锁终点
    /// </summary>
    Task<(bool IsUnlocked, decimal UsedEnergy, decimal StoredEnergyMeters)> UnlockWithEnergyAsync(long userId, int startLocationId, int endLocationId);

    /// <summary>
    /// 获取玩家已解锁的点位列表
    /// </summary>
    Task<List<PlayerUnlockedLocation>> GetPlayerUnlockedLocationsAsync(long userId);

    /// <summary>
    /// 获取玩家已完成的点位列表
    /// </summary>
    Task<List<PlayerCompletedLocation>> GetPlayerCompletedLocationsAsync(long userId);

    /// <summary>
    /// 获取玩家的所有路线进度记录
    /// </summary>
    Task<List<PlayerMapProgress>> GetPlayerProgressAsync(long userId);

    /// <summary>
    /// 获取玩家存储能量（米）
    /// </summary>
    Task<decimal> GetPlayerStoredEnergyMetersAsync(long userId);
}

/// <summary>
/// 地图点位访问结果
/// </summary>
public class MapLocationVisitResult
{
    /// <summary>
    /// 是否首次访问
    /// </summary>
    public bool IsFirstVisit { get; set; }

    /// <summary>
    /// 是否消耗了道具（当 isCompleted=false 且配置了 Consumption 时为 true）
    /// </summary>
    public bool DidConsumeItem { get; set; }

    /// <summary>
    /// 奖励列表 [[物品ID, 数量], ...]
    /// </summary>
    public List<List<int>>? Rewards { get; set; }

    /// <summary>
    /// 访问记录
    /// </summary>
    public PlayerMapLocationVisit? VisitRecord { get; set; }

    /// <summary>
    /// 地图配置信息
    /// </summary>
    public MapBaseConfig? MapConfig { get; set; }
}

public class MapService : IMapService
{
    private const decimal MaxStoredEnergyMeters = 10000m;

    private readonly AppDbContext _dbContext;
    private readonly IMapConfigService _mapConfigService;
    private readonly IInventoryService _inventoryService;
    private readonly IPlayerRoleService _playerRoleService;

    public MapService(
        AppDbContext dbContext,
        IMapConfigService mapConfigService,
        IInventoryService inventoryService,
        IPlayerRoleService playerRoleService)
    {
        _dbContext = dbContext;
        _mapConfigService = mapConfigService;
        _inventoryService = inventoryService;
        _playerRoleService = playerRoleService;
    }

    public async Task<(PlayerMapProgress Progress, bool IsUnlock, decimal StoredEnergyMeters)> SaveMapProgressAsync(
        long userId,
        int startLocationId,
        int endLocationId,
        decimal distanceMeters)
    {
        var progress = await _dbContext.PlayerMapProgress.FirstOrDefaultAsync(p =>
            p.UserId == userId &&
            p.StartLocationId == startLocationId &&
            p.EndLocationId == endLocationId);

        var previousDistance = progress?.DistanceMeters ?? 0m;

        if (progress != null)
        {
            progress.DistanceMeters = distanceMeters;
            progress.CreatedAt = DateTime.UtcNow;
            Log.Information(
                "Updated map progress for user {UserId}: {Start} -> {End}, Distance: {Distance}m",
                userId, startLocationId, endLocationId, distanceMeters);
        }
        else
        {
            progress = new PlayerMapProgress
            {
                UserId = userId,
                StartLocationId = startLocationId,
                EndLocationId = endLocationId,
                DistanceMeters = distanceMeters,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.PlayerMapProgress.Add(progress);
            Log.Information(
                "Saved new map progress for user {UserId}: {Start} -> {End}, Distance: {Distance}m",
                userId, startLocationId, endLocationId, distanceMeters);
        }

        // 处理解锁与存储能量
        var isUnlock = false;
        var endLocationConfig = _mapConfigService.GetMapConfigByLocationId(endLocationId);
        decimal addEnergy = 0m;
        if (endLocationConfig != null && endLocationConfig.UnlockDistance.HasValue && endLocationConfig.UnlockDistance > 0)
        {
            var unlockDist = endLocationConfig.UnlockDistance.Value;
            var prevExcess = Math.Max(0m, previousDistance - unlockDist);
            var newExcess = Math.Max(0m, distanceMeters - unlockDist);
            addEnergy = Math.Max(0m, newExcess - prevExcess);

            if (distanceMeters >= unlockDist)
            {
                // 检查是否已经解锁过
                var alreadyUnlocked = await _dbContext.PlayerUnlockedLocation
                    .AnyAsync(u => u.UserId == userId && u.LocationId == endLocationId);

                if (!alreadyUnlocked)
                {
                    _dbContext.PlayerUnlockedLocation.Add(new PlayerUnlockedLocation
                    {
                        UserId = userId,
                        LocationId = endLocationId,
                        UnlockedTime = DateTime.UtcNow
                    });
                    isUnlock = true;
                    Log.Information(
                        "User {UserId} unlocked location {LocationId} by reaching distance {Distance}m",
                        userId, endLocationId, distanceMeters);
                }
            }
        }

        // 累加玩家存储能量并限幅
        var player = await _playerRoleService.GetOrCreatePlayerAsync(userId);
        if (addEnergy > 0)
        {
            var before = player.StoredEnergyMeters;
            player.StoredEnergyMeters = Math.Min(MaxStoredEnergyMeters, before + addEnergy);
            if (player.StoredEnergyMeters != before)
            {
                Log.Information("User {UserId} gained stored energy: +{Add}m -> {Total}m", userId, addEnergy, player.StoredEnergyMeters);
            }
        }

        await _dbContext.SaveChangesAsync();

        return (progress, isUnlock, player.StoredEnergyMeters);
    }

    public async Task<MapLocationVisitResult> VisitMapLocationAsync(long userId, int locationId, bool isCompleted)
    {
        var mapConfig = _mapConfigService.GetMapConfigByLocationId(locationId);
        if (mapConfig == null)
        {
            throw new ArgumentException($"Location {locationId} not found in map configuration");
        }

        Log.Information("Visiting location {LocationId}, configured consumption: [{Consumption}]", locationId, mapConfig.Consumption != null ? string.Join(", ", mapConfig.Consumption) : "null");

        var didConsume = false;


        if (!isCompleted && mapConfig.Consumption is { Count: 2 } consumption && consumption[1] > 0)
        {
            var itemId = consumption[0];
            var amount = consumption[1];
            try
            {
                await _inventoryService.ConsumeItemAsync(userId, itemId, amount, ct: default);
                didConsume = true;
                Log.Information("User {UserId} consumed item {ItemId} x{Amount} for location {LocationId}", userId, itemId, amount, locationId);
            }
            catch (ArgumentException ex)
            {
                Log.Warning("User {UserId} has insufficient items ({ItemId} x{Amount}) for location {LocationId}: {Message}", userId, itemId, amount, locationId, ex.Message);
                throw new InvalidOperationException("物品不足");
            }
        }

        var existingVisit = await _dbContext.PlayerMapLocationVisit
            .FirstOrDefaultAsync(v => v.UserId == userId && v.LocationId == locationId);

        PlayerMapLocationVisit visitRecord;

        if (existingVisit == null)
        {
            visitRecord = new PlayerMapLocationVisit
            {
                UserId = userId,
                LocationId = locationId,
                FirstVisitTime = DateTime.UtcNow,
                VisitCount = 1,
                LastVisitTime = DateTime.UtcNow
            };

            _dbContext.PlayerMapLocationVisit.Add(visitRecord);
            Log.Information("User {UserId} first visit to location {LocationId}", userId, locationId);
        }
        else
        {
            existingVisit.VisitCount++;
            existingVisit.LastVisitTime = DateTime.UtcNow;
            visitRecord = existingVisit;
            Log.Information("User {UserId} revisit location {LocationId}, count: {Count}",
                userId, locationId, visitRecord.VisitCount);
        }

        var isFirstVisit = existingVisit == null;

        if (isCompleted)
        {
            var hasCompleted = await _dbContext.PlayerCompletedLocation
                .AnyAsync(c => c.UserId == userId && c.LocationId == locationId);
            if (!hasCompleted)
            {
                _dbContext.PlayerCompletedLocation.Add(new PlayerCompletedLocation
                {
                    UserId = userId,
                    LocationId = locationId,
                    CompletedTime = DateTime.UtcNow
                });
            }
        }

        await _dbContext.SaveChangesAsync();

        List<List<int>>? rewards = null;
        if (isFirstVisit && mapConfig.FirstReward != null)
        {
            rewards ??= new List<List<int>>();
            rewards.AddRange(mapConfig.FirstReward);
        }
        if (isCompleted && mapConfig.FixedReward != null)
        {
            rewards ??= new List<List<int>>();
            rewards.AddRange(mapConfig.FixedReward);
        }

        if (rewards != null && rewards.Count > 0)
        {
            foreach (var r in rewards)
            {
                if (r.Count >= 2)
                {
                    var itemId = r[0];
                    var amount = r[1];
                    await _inventoryService.GrantItemAsync(userId, itemId, amount);
                }
            }
            Log.Information("Granted {Count} rewards to user {UserId} for location {LocationId}", rewards.Count, userId, locationId);
        }

        return new MapLocationVisitResult
        {
            IsFirstVisit = isFirstVisit,
            DidConsumeItem = didConsume,
            Rewards = rewards,
            VisitRecord = visitRecord,
            MapConfig = mapConfig
        };
    }

    public async Task<List<PlayerUnlockedLocation>> GetPlayerUnlockedLocationsAsync(long userId)
    {
        return await _dbContext.PlayerUnlockedLocation
            .Where(u => u.UserId == userId)
            .OrderByDescending(u => u.UnlockedTime)
            .ToListAsync();
    }

    public async Task<List<PlayerCompletedLocation>> GetPlayerCompletedLocationsAsync(long userId)
    {
        return await _dbContext.PlayerCompletedLocation
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CompletedTime)
            .ToListAsync();
    }

    public async Task<List<PlayerMapProgress>> GetPlayerProgressAsync(long userId)
    {
        return await _dbContext.PlayerMapProgress
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<(bool IsUnlocked, decimal UsedEnergy, decimal StoredEnergyMeters)> UnlockWithEnergyAsync(long userId, int startLocationId, int endLocationId)
    {
        var endConfig = _mapConfigService.GetMapConfigByLocationId(endLocationId);
        if (endConfig == null || !endConfig.UnlockDistance.HasValue || endConfig.UnlockDistance.Value <= 0)
        {
            // 没有解锁需求，视为可解锁且不消耗能量
            var already = await _dbContext.PlayerUnlockedLocation.AnyAsync(u => u.UserId == userId && u.LocationId == endLocationId);
            if (!already)
            {
                _dbContext.PlayerUnlockedLocation.Add(new PlayerUnlockedLocation
                {
                    UserId = userId,
                    LocationId = endLocationId,
                    UnlockedTime = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();
            }
            var player0 = await _playerRoleService.GetOrCreatePlayerAsync(userId);
            return (true, 0m, player0.StoredEnergyMeters);
        }

        var unlockDist = endConfig.UnlockDistance.Value;
        var progress = await _dbContext.PlayerMapProgress.FirstOrDefaultAsync(p => p.UserId == userId && p.StartLocationId == startLocationId && p.EndLocationId == endLocationId);
        var current = progress?.DistanceMeters ?? 0m;
        if (current >= unlockDist)
        {
            // 已满足，确保已解锁
            var already = await _dbContext.PlayerUnlockedLocation.AnyAsync(u => u.UserId == userId && u.LocationId == endLocationId);
            if (!already)
            {
                _dbContext.PlayerUnlockedLocation.Add(new PlayerUnlockedLocation
                {
                    UserId = userId,
                    LocationId = endLocationId,
                    UnlockedTime = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();
            }
            var player1 = await _playerRoleService.GetOrCreatePlayerAsync(userId);
            return (true, 0m, player1.StoredEnergyMeters);
        }

        var need = unlockDist - current;
        var player = await _playerRoleService.GetOrCreatePlayerAsync(userId);
        if (player.StoredEnergyMeters >= need)
        {
            player.StoredEnergyMeters -= need;
            // 解锁
            var already = await _dbContext.PlayerUnlockedLocation.AnyAsync(u => u.UserId == userId && u.LocationId == endLocationId);
            if (!already)
            {
                _dbContext.PlayerUnlockedLocation.Add(new PlayerUnlockedLocation
                {
                    UserId = userId,
                    LocationId = endLocationId,
                    UnlockedTime = DateTime.UtcNow
                });
            }
            await _dbContext.SaveChangesAsync();
            Log.Information("User {UserId} unlocked location {LocationId} by spending stored energy {Used}m, remain {Remain}m", userId, endLocationId, need, player.StoredEnergyMeters);
            return (true, need, player.StoredEnergyMeters);
        }

        // 能量不足，返回未解锁
        Log.Information("User {UserId} insufficient stored energy to unlock {LocationId}. Need {Need}m, have {Have}m", userId, endLocationId, need, player.StoredEnergyMeters);
        return (false, 0m, player.StoredEnergyMeters);
    }

    public async Task<decimal> GetPlayerStoredEnergyMetersAsync(long userId)
    {
        var player = await _playerRoleService.GetOrCreatePlayerAsync(userId);
        return player.StoredEnergyMeters;
    }
}
