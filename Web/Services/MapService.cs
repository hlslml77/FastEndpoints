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
    Task<MapLocationVisitResult> VisitMapLocationAsync(long userId, int locationId);

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
    private readonly ILogger<MapService> _logger;

    public MapService(
        AppDbContext dbContext,
        IMapConfigService mapConfigService,
        ILogger<MapService> logger)
    {
        _dbContext = dbContext;
        _mapConfigService = mapConfigService;
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

        // 如果进度达到或超过配置的线路距离，则将起点与终点标记为完成
        var startConfig = _mapConfigService.GetMapConfigByLocationId(startLocationId);
        int? requiredDistance = null;
        if (startConfig?.TheNextPointDistance != null)
        {
            var pair = startConfig.TheNextPointDistance.FirstOrDefault(p => p.Count >= 2 && p[0] == endLocationId);
            if (pair != null && pair.Count >= 2)
                requiredDistance = pair[1];
        }

        if (requiredDistance.HasValue && distanceMeters >= requiredDistance.Value)
        {
            if (!await _dbContext.PlayerCompletedLocation.AnyAsync(c => c.UserId == userId && c.LocationId == startLocationId))
            {
                _dbContext.PlayerCompletedLocation.Add(new PlayerCompletedLocation
                {
                    UserId = userId,
                    LocationId = startLocationId,
                    CompletedTime = DateTime.UtcNow
                });
            }
            if (!await _dbContext.PlayerCompletedLocation.AnyAsync(c => c.UserId == userId && c.LocationId == endLocationId))
            {
                _dbContext.PlayerCompletedLocation.Add(new PlayerCompletedLocation
                {
                    UserId = userId,
                    LocationId = endLocationId,
                    CompletedTime = DateTime.UtcNow
                });
            }
            _logger.LogInformation("Marked start {Start} and end {End} as completed for user {UserId}", startLocationId, endLocationId, userId);
        }

        await _dbContext.SaveChangesAsync();

        return progress;
    }

    public async Task<MapLocationVisitResult> VisitMapLocationAsync(long userId, int locationId)
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

        await _dbContext.SaveChangesAsync();

        // 根据是否首次访问返回不同的奖励
        var rewards = isFirstVisit ? mapConfig.FirstReward : mapConfig.FixedReward;

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
