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
        var progress = new PlayerMapProgress
        {
            UserId = userId,
            StartLocationId = startLocationId,
            EndLocationId = endLocationId,
            DistanceMeters = distanceMeters,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PlayerMapProgress.Add(progress);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Saved map progress for user {UserId}: {Start} -> {End}, Distance: {Distance}m",
            userId, startLocationId, endLocationId, distanceMeters);

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

        bool isFirstVisit = existingVisit == null;

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
}

