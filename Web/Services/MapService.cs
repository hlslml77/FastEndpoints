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
    Task<MapLocationVisitResult> VisitMapLocationAsync(long userId, int locationId, bool isCompleted, bool needConsume);

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

    /// <summary>
    /// 获取或生成今日随机事件
    /// </summary>
    Task<List<PlayerDailyRandomEvent>> GetOrGenerateTodayRandomEventsAsync(long userId);

    /// <summary>
    /// 完成某个随机事件，发放奖励并处理消耗
    /// </summary>
    Task<(bool Success, List<List<int>>? Rewards)> CompleteRandomEventAsync(long userId, int locationId, int? eventId);

    /// <summary>
    /// 获取玩家当前所在点位
    /// </summary>
    Task<int?> GetCurrentLocationIdAsync(long userId);

    /// <summary>
    /// 统计在指定点位的玩家人数（基于当前所在点位），并返回玩家的下次挑战时间
    /// </summary>
    Task<(int PeopleCount, DateTime? NextChallengeTime)> CountPlayersAtLocationAsync(long userId, int locationId);
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
    private readonly AppDbContext _dbContext;
    private readonly IMapConfigService _mapConfigService;
    private readonly IInventoryService _inventoryService;
    private readonly IPlayerRoleService _playerRoleService;
    private readonly IGeneralConfigService _generalConfigService;

    private readonly IRandomWorldEventConfigService _randomCfg;
    private readonly Random _rand = new();

    public MapService(
        AppDbContext dbContext,
        IMapConfigService mapConfigService,
        IInventoryService inventoryService,
        IPlayerRoleService playerRoleService,
        IGeneralConfigService generalConfigService,
        IRandomWorldEventConfigService randomCfg)
    {
        _dbContext = dbContext;
        _mapConfigService = mapConfigService;
        _inventoryService = inventoryService;
        _playerRoleService = playerRoleService;
        _generalConfigService = generalConfigService;
        _randomCfg = randomCfg;
    }

    private int? GetRequiredDistanceFromStartToEnd(int startLocationId, int endLocationId)
    {
        var startCfg = _mapConfigService.GetMapConfigByLocationId(startLocationId);
        var list = startCfg?.TheNextPointDistance;
        if (list == null) return null;
        foreach (var pair in list)
        {
            if (pair.Count >= 2 && pair[0] == endLocationId)
                return pair[1];
        }
        return null;
    }

    private async Task SetCurrentLocationAsync(long userId, int locationId)
    {
        var player = await _playerRoleService.GetOrCreatePlayerAsync(userId);
        if (player.CurrentLocationId != locationId)
        {
            player.CurrentLocationId = locationId;
            player.LastUpdateTime = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// 增加指定点位的人数统计
    /// </summary>
    private async Task IncrementLocationPeopleCountAsync(int locationId)
    {
        var record = await _dbContext.LocationPeopleCount.FirstOrDefaultAsync(p => p.LocationId == locationId);
        if (record != null)
        {
            record.PeopleCount++;
            record.LastUpdateTime = DateTime.UtcNow;
        }
        else
        {
            _dbContext.LocationPeopleCount.Add(new LocationPeopleCount
            {
                LocationId = locationId,
                PeopleCount = 1,
                LastUpdateTime = DateTime.UtcNow
            });
        }
        Log.Information("Incremented people count for location {LocationId}", locationId);
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

        // 若跑步距离达到起点->终点所需距离，则更新当前点位为终点，并增加该点位人数
        var required = GetRequiredDistanceFromStartToEnd(startLocationId, endLocationId);
        if (required.HasValue && distanceMeters >= required.Value)
        {
            await SetCurrentLocationAsync(userId, endLocationId);
            // 增加终点位置的人数统计
            await IncrementLocationPeopleCountAsync(endLocationId);
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
            var cap = _generalConfigService.GetStoredEnergyMaxMeters();
            player.StoredEnergyMeters = Math.Min(cap, before + addEnergy);
            if (player.StoredEnergyMeters != before)
            {
                Log.Information("User {UserId} gained stored energy: +{Add}m -> {Total}m (cap={Cap}m)", userId, addEnergy, player.StoredEnergyMeters, cap);
            }
        }

        await _dbContext.SaveChangesAsync();

        return (progress, isUnlock, player.StoredEnergyMeters);
    }

    public async Task<MapLocationVisitResult> VisitMapLocationAsync(long userId, int locationId, bool isCompleted, bool needConsume)
    {
        var mapConfig = _mapConfigService.GetMapConfigByLocationId(locationId);
        if (mapConfig == null)
        {
            throw new ArgumentException($"Location {locationId} not found in map configuration");
        }

        Log.Information("Visiting location {LocationId}, configured consumption: [{Consumption}]", locationId, mapConfig.Consumption != null ? string.Join(", ", mapConfig.Consumption) : "null");

        var didConsume = false;


        if (needConsume && mapConfig.Consumption is { Count: 2 } consumption && consumption[1] > 0)
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

        // 更新玩家当前所在点位为本次访问的点位，并增加该点位人数
        await SetCurrentLocationAsync(userId, locationId);
        await IncrementLocationPeopleCountAsync(locationId);

        if (isCompleted)
        {
            var hasCompleted = await _dbContext.PlayerCompletedLocation
                .FirstOrDefaultAsync(c => c.UserId == userId && c.LocationId == locationId);

            DateTime? nextChallengeTime = null;

            // 如果该点位有资源倒计时，计算下次可挑战时间（直接从 MapBaseConfig 读取 RefreshTime）
            if (mapConfig.Resources > 0)
            {
                var refreshTimeHours = mapConfig.RefreshTime;
                if (refreshTimeHours.HasValue && refreshTimeHours.Value > 0)
                {
                    nextChallengeTime = DateTime.UtcNow.AddHours(refreshTimeHours.Value);
                    Log.Information("User {UserId} completed location {LocationId} with resource {ResourceId}, next challenge time: {NextChallengeTime}",
                        userId, locationId, mapConfig.Resources, nextChallengeTime);
                }
            }

            if (hasCompleted == null)
            {
                _dbContext.PlayerCompletedLocation.Add(new PlayerCompletedLocation
                {
                    UserId = userId,
                    LocationId = locationId,
                    CompletedTime = DateTime.UtcNow,
                    NextChallengeTime = nextChallengeTime
                });
            }
            else
            {
                // 更新倒计时时间
                hasCompleted.NextChallengeTime = nextChallengeTime;
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


    public async Task<List<PlayerDailyRandomEvent>> GetOrGenerateTodayRandomEventsAsync(long userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await _dbContext.PlayerDailyRandomEvent
            .Where(e => e.UserId == userId && e.Date == today)
            .ToListAsync();

        var desired = _generalConfigService.GetDailyRandomEventCount();
        if (desired <= 0) desired = 1;
        if (existing.Count >= desired)
            return existing.OrderBy(e => e.Id).ToList();

        // 需要生成补齐
        var unlocked = await GetPlayerUnlockedLocationsAsync(userId);
        var unlockedSet = new HashSet<int>(unlocked.Select(u => u.LocationId));
        var points = _randomCfg.GetRandomPointConfigs();
        var candidates = points
            .Where(p => unlockedSet.Contains(p.PositioningPoint))
            .Select(p => p.PositioningPoint)
            .Distinct()
            .ToList();

        var existingLocs = new HashSet<int>(existing.Select(e => e.LocationId));
        var availableLocs = candidates.Where(c => !existingLocs.Contains(c)).ToList();

        // 若可用点位不足，按可用数量为准
        var toCreateCount = Math.Min(desired - existing.Count, Math.Max(0, availableLocs.Count));
        if (toCreateCount <= 0)
            return existing.OrderBy(e => e.Id).ToList();

        var eventsCfg = _randomCfg.GetEventConfigs();
        if (eventsCfg.Count == 0)
            return existing.OrderBy(e => e.Id).ToList();

        RandomEventConfigEntry PickWeighted()
        {
            var total = Math.Max(1, eventsCfg.Sum(e => Math.Max(0, e.Probability)));
            var roll = _rand.Next(1, total + 1);
            var acc = 0;
            foreach (var e in eventsCfg)
            {
                acc += Math.Max(0, e.Probability);
                if (roll <= acc) return e;
            }
            return eventsCfg[^1];
        }

        var newRecords = new List<PlayerDailyRandomEvent>();
        for (int i = 0; i < toCreateCount; i++)
        {
            if (availableLocs.Count == 0) break;
            var idx = _rand.Next(availableLocs.Count);
            var loc = availableLocs[idx];
            availableLocs.RemoveAt(idx);
            var ev = PickWeighted();
            var rec = new PlayerDailyRandomEvent
            {
                UserId = userId,
                Date = today,
                LocationId = loc,
                EventId = ev.ID,
                IsCompleted = false,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.PlayerDailyRandomEvent.Add(rec);
            newRecords.Add(rec);
        }

        if (newRecords.Count > 0)
        {
            await _dbContext.SaveChangesAsync();
            existing.AddRange(newRecords);
        }

        return existing.OrderBy(e => e.Id).ToList();
    }

    public async Task<(bool Success, List<List<int>>? Rewards)> CompleteRandomEventAsync(long userId, int locationId, int? eventId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rec = await _dbContext.PlayerDailyRandomEvent
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Date == today && r.LocationId == locationId);
        if (rec == null) return (false, null);
        if (eventId.HasValue && rec.EventId != eventId.Value) return (false, null);
        if (rec.IsCompleted) return (true, null);

        var ev = _randomCfg.GetEventById(rec.EventId);
        if (ev == null) return (false, null);

        // 检查并扣除消耗（先校验再扣除，避免部分扣除）
        if (ev.Consumption is { Count: > 0 })
        {
            // 聚合所需数量
            var need = new Dictionary<int, int>();
            foreach (var c in ev.Consumption)
            {
                if (c.Count >= 2 && c[1] > 0)
                {
                    var itemId = c[0];
                    var amount = c[1];
                    if (need.ContainsKey(itemId)) need[itemId] += amount; else need[itemId] = amount;
                }
            }
            if (need.Count > 0)
            {
                // 查询库存
                var items = await _dbContext.PlayerItem.Where(x => x.UserId == userId).ToListAsync();
                foreach (var kv in need)
                {
                    var have = items.FirstOrDefault(i => i.ItemId == kv.Key)?.Amount ?? 0;
                    if (have < kv.Value)
                        return (false, null);
                }
                // 扣除
                foreach (var kv in need)
                {
                    await _inventoryService.ConsumeItemAsync(userId, kv.Key, kv.Value);
                }
            }
        }

        // 发放奖励
        var rewards = ev.FixedReward != null ? new List<List<int>>(ev.FixedReward) : null;
        if (rewards != null)
        {
            foreach (var r in rewards)
            {
                if (r.Count >= 2 && r[1] > 0)
                {
                    await _inventoryService.GrantItemAsync(userId, r[0], r[1]);
                }
            }
        }

        rec.IsCompleted = true;
        rec.CompletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return (true, rewards);
    }

    public async Task<decimal> GetPlayerStoredEnergyMetersAsync(long userId)
    {
        var player = await _playerRoleService.GetOrCreatePlayerAsync(userId);
        return player.StoredEnergyMeters;
    }

    public async Task<int?> GetCurrentLocationIdAsync(long userId)
    {
        var player = await _playerRoleService.GetOrCreatePlayerAsync(userId);
        return player.CurrentLocationId;
    }

    public async Task<(int PeopleCount, DateTime? NextChallengeTime)> CountPlayersAtLocationAsync(long userId, int locationId)
    {
        // 获取该点位的人数统计记录
        var record = await _dbContext.LocationPeopleCount.FirstOrDefaultAsync(p => p.LocationId == locationId);
        var count = record?.PeopleCount ?? 0;

        if (count == 0)
        {
            // 当人数为0时，返回配置的机器人显示数量范围内的随机数
            var range = _generalConfigService.GetRobotDisplayRange();
            var min = range.min;
            var max = range.max;
            if (max <= 0) count = 0;
            else
            {
                if (min < 0) min = 0;
                if (max < min) (min, max) = (max, min);
                count = _rand.Next(min, max + 1);
            }
        }

        // 获取玩家该点位的下次挑战时间
        var completedLocation = await _dbContext.PlayerCompletedLocation
            .FirstOrDefaultAsync(c => c.UserId == userId && c.LocationId == locationId);
        var nextChallengeTime = completedLocation?.NextChallengeTime;

        return (count, nextChallengeTime);
    }
}
