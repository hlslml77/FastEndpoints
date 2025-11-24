using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Data.Config;
using Web.Data.Entities;

namespace Web.Services;

/// <summary>
/// 地图服务接口
/// </summary>
public interface IMapService
{
    /// <summary>
    /// 保存地图进度
    /// </summary>
    Task<PlayerMapProgress> SaveMapProgressAsync(long userId, int startLocationId, int endLocationId, decimal distanceMeters);

    /// <summary>
    /// 访问地图点位，返回是否首次访问和奖励信息
    /// </summary>
    Task<MapLocationVisitResult> VisitMapLocationAsync(long userId, int locationId, bool isCompleted);

    /// <summary>
    /// 获取玩家的地图访问记录
    /// </summary>
    Task<List<PlayerMapLocationVisit>> GetPlayerVisitedLocationsAsync(long userId);

    /// <summary>
    /// 获取玩家已完成的点位列表
    /// </summary>
    Task<List<PlayerCompletedLocation>> GetPlayerCompletedLocationsAsync(long userId);

    /// <summary>
    /// 获取玩家的所有路线进度记录
    /// </summary>
    Task<List<PlayerMapProgress>> GetPlayerProgressAsync(long userId);

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
    private readonly AppDbContext _dbContext;
    private readonly IMapConfigService _mapConfigService;
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<MapService> _logger;

    public MapService(
        AppDbContext dbContext,
        IMapConfigService mapConfigService,
        IInventoryService inventoryService,
        ILogger<MapService> logger)
    {
        _dbContext = dbContext;
        _mapConfigService = mapConfigService;
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task<PlayerMapProgress> SaveMapProgressAsync(
        long userId,
        int startLocationId,
        int endLocationId,
        decimal distanceMeters)
    {
        var progress = await _dbContext.PlayerMapProgress.FirstOrDefaultAsync(p =>
            p.UserId == userId &&
            p.StartLocationId == startLocationId &&
            p.EndLocationId == endLocationId);

        if (progress != null)
        {
            // 更新现有进度
            progress.DistanceMeters = distanceMeters;
            progress.CreatedAt = DateTime.UtcNow; // 更新时间
            _logger.LogInformation(
                "Updated map progress for user {UserId}: {Start} -> {End}, Distance: {Distance}m",
                userId, startLocationId, endLocationId, distanceMeters);
        }
        else
        {
            // 插入新进度
            progress = new PlayerMapProgress
            {
                UserId = userId,
                StartLocationId = startLocationId,
                EndLocationId = endLocationId,
                DistanceMeters = distanceMeters,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.PlayerMapProgress.Add(progress);
            _logger.LogInformation(
                "Saved new map progress for user {UserId}: {Start} -> {End}, Distance: {Distance}m",
                userId, startLocationId, endLocationId, distanceMeters);
        }

        // 不再由服务器根据距离自动判定完成。是否完成由客户端在 /map/visit-location 上报。
        // 因此此处仅保存进度，不写入 PlayerCompletedLocation。

        await _dbContext.SaveChangesAsync();

        return progress;
    }

    public async Task<MapLocationVisitResult> VisitMapLocationAsync(long userId, int locationId, bool isCompleted)
    {
        // 获取地图配置
        var mapConfig = _mapConfigService.GetMapConfigByLocationId(locationId);
        if (mapConfig == null)
        {
            throw new ArgumentException($"Location {locationId} not found in map configuration");
        }

        // 查找是否已访问过
        var existingVisit = await _dbContext.PlayerMapLocationVisit
            .FirstOrDefaultAsync(v => v.UserId == userId && v.LocationId == locationId);

        PlayerMapLocationVisit visitRecord;

        if (existingVisit == null)
        {
            // 首次访问
            visitRecord = new PlayerMapLocationVisit
            {
                UserId = userId,
                LocationId = locationId,
                FirstVisitTime = DateTime.UtcNow,
                VisitCount = 1,
                LastVisitTime = DateTime.UtcNow
            };

            _dbContext.PlayerMapLocationVisit.Add(visitRecord);
            _logger.LogInformation("User {UserId} first visit to location {LocationId}", userId, locationId);
        }
        else
        {
            // 非首次访问
            existingVisit.VisitCount++;
            existingVisit.LastVisitTime = DateTime.UtcNow;
            visitRecord = existingVisit;
            _logger.LogInformation("User {UserId} revisit location {LocationId}, count: {Count}",
                userId, locationId, visitRecord.VisitCount);
        }

        var isFirstVisit = existingVisit == null;

        // 如果客户端上报完成，则记录到完成点位表
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

        // 奖励规则：
        // - 首次访问发首次奖励
        // - 完成发完成奖励（目前使用 FixedReward 作为完成奖励）
        // 两者可以同时发放，奖励合并返回
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

        // 发放奖励到背包与装备表（依据物品配置由 InventoryService 判定）
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
            _logger.LogInformation("Granted {Count} rewards to user {UserId} for location {LocationId}", rewards.Count, userId, locationId);
        }

        return new MapLocationVisitResult
        {
            IsFirstVisit = isFirstVisit,
            Rewards = rewards,
            VisitRecord = visitRecord,
            MapConfig = mapConfig
        };
    }

    public async Task<List<PlayerMapLocationVisit>> GetPlayerVisitedLocationsAsync(long userId)
    {
        return await _dbContext.PlayerMapLocationVisit
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.LastVisitTime)
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
}
