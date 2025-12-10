using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
using Web.Data;
using Web.Data.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace Web.Services;

public interface IPveRankService
{
    Task AddSportAsync(long userId, int deviceType, decimal distanceMeters, DateTime nowUtc);

    Task<(List<RankItem> top, RankItem? me)> GetLeaderboardAsync(int periodType, int deviceType, int topN, long? userId);

    Task<(bool success, string message, List<(int itemId,int amount)> rewards)> ClaimWeeklyAsync(long userId, int deviceType, DateTime nowUtc);
    Task<(bool success, string message, List<(int itemId,int amount)> rewards)> ClaimSeasonAsync(long userId, int deviceType, DateTime nowUtc);
}

public record RankItem(long UserId, decimal TotalDistanceMeters, int Rank);

public class PveRankService : IPveRankService
{
    private readonly AppDbContext _db;
    private readonly IPveRankConfigService _cfg;
    private readonly IInventoryService _inventory;
    private readonly IMemoryCache _mem;
    private readonly IDistributedCache? _dist;

    private static readonly TimeSpan TopCacheTtl = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MeCacheTtl = TimeSpan.FromSeconds(8);

    public PveRankService(AppDbContext db, IPveRankConfigService cfg, IInventoryService inventory, IMemoryCache mem, IServiceProvider sp)
    {
        _db = db; _cfg = cfg; _inventory = inventory; _mem = mem; _dist = sp.GetService<IDistributedCache>();
    }

    public async Task AddSportAsync(long userId, int deviceType, decimal distanceMeters, DateTime nowUtc)
    {
        // 1) 更新每日汇总
        var today = DateOnly.FromDateTime(nowUtc);
        var daily = await _db.PlayerSportDaily.FindAsync(userId, today, deviceType);
        if (daily == null)
        {
            daily = new PlayerSportDaily { UserId = userId, Date = today, DeviceType = deviceType, DistanceMeters = 0, Calories = 0, UpdatedAt = nowUtc };
            _db.PlayerSportDaily.Add(daily);
        }
        daily.DistanceMeters += distanceMeters;
        daily.UpdatedAt = nowUtc;

        // 2) 更新周榜/赛季榜
        var (weekType, weekId) = GetWeekPeriod(nowUtc);
        await UpsertBoardAsync(weekType, weekId, deviceType, userId, distanceMeters, nowUtc);

        var (seasonType, seasonId) = GetSeasonPeriod(nowUtc);
        await UpsertBoardAsync(seasonType, seasonId, deviceType, userId, distanceMeters, nowUtc);

        await _db.SaveChangesAsync();
    }

    private async Task UpsertBoardAsync(int periodType, int periodId, int deviceType, long userId, decimal addMeters, DateTime nowUtc)
    {
        // 根据数据库提供程序选择合适的 UPSERT 语法；失败则回退到 EF 方式，避免崩溃
        var provider = _db.Database.ProviderName ?? string.Empty;
        try
        {
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                var sql = @"INSERT INTO pve_rank_board
(period_type, period_id, device_type, user_id, total_distance_meters, updated_at)
VALUES ({0}, {1}, {2}, {3}, {4}, {5})
ON CONFLICT(period_type, period_id, device_type, user_id)
DO UPDATE SET total_distance_meters = total_distance_meters + EXCLUDED.total_distance_meters,
              updated_at = EXCLUDED.updated_at;";
                await _db.Database.ExecuteSqlRawAsync(sql, periodType, periodId, deviceType, userId, addMeters, nowUtc);
            }
            else if (provider.Contains("MySql", StringComparison.OrdinalIgnoreCase) || provider.Contains("Pomelo", StringComparison.OrdinalIgnoreCase))
            {
                var sql = @"INSERT INTO pve_rank_board
(period_type, period_id, device_type, user_id, total_distance_meters, updated_at)
VALUES ({0}, {1}, {2}, {3}, {4}, {5})
ON DUPLICATE KEY UPDATE total_distance_meters = total_distance_meters + VALUES(total_distance_meters),
                        updated_at = VALUES(updated_at);";
                await _db.Database.ExecuteSqlRawAsync(sql, periodType, periodId, deviceType, userId, addMeters, nowUtc);
            }
            else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                var sql = @"MERGE pve_rank_board AS t
USING (SELECT {0} AS period_type, {1} AS period_id, {2} AS device_type, {3} AS user_id, {4} AS total_distance_meters, {5} AS updated_at) AS s
ON (t.period_type=s.period_type AND t.period_id=s.period_id AND t.device_type=s.device_type AND t.user_id=s.user_id)
WHEN MATCHED THEN UPDATE SET t.total_distance_meters = t.total_distance_meters + s.total_distance_meters, t.updated_at = s.updated_at
WHEN NOT MATCHED THEN INSERT (period_type, period_id, device_type, user_id, total_distance_meters, updated_at)
VALUES (s.period_type, s.period_id, s.device_type, s.user_id, s.total_distance_meters, s.updated_at);";
                await _db.Database.ExecuteSqlRawAsync(sql, periodType, periodId, deviceType, userId, addMeters, nowUtc);
            }
            else
            {
                // 未识别的提供程序，使用 EF 回退
                await EfFallbackUpsert(periodType, periodId, deviceType, userId, addMeters, nowUtc);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "UpsertBoard raw SQL failed for provider {Provider}. Falling back to EF.", provider);
            await EfFallbackUpsert(periodType, periodId, deviceType, userId, addMeters, nowUtc);
        }

        // 失效短缓存（内存 & 分布式）。TTL 很短，失效非必须，这里尽量清理热点 Top 数
        var keysTop = new[] { 10, 20, 50, 100 };
        foreach (var top in keysTop)
        {
            var k1 = TopCacheKey(periodType, periodId, deviceType, top);
            _mem.Remove(k1);
            if (_dist != null) await _dist.RemoveAsync(k1);
        }
        var meKey = MeCacheKey(periodType, periodId, deviceType, userId);
        _mem.Remove(meKey);
        if (_dist != null) await _dist.RemoveAsync(meKey);
    }

    private async Task EfFallbackUpsert(int periodType, int periodId, int deviceType, long userId, decimal addMeters, DateTime nowUtc)
    {
        var row = await _db.PveRankBoard.FindAsync(periodType, periodId, deviceType, userId);
        if (row == null)
        {
            row = new PveRankBoard
            {
                PeriodType = periodType,
                PeriodId = periodId,
                DeviceType = deviceType,
                UserId = userId,
                TotalDistanceMeters = addMeters,
                UpdatedAt = nowUtc
            };
            _db.PveRankBoard.Add(row);
        }
        else
        {
            row.TotalDistanceMeters += addMeters;
            row.UpdatedAt = nowUtc;
            _db.PveRankBoard.Update(row);
        }
        await _db.SaveChangesAsync();
    }

    public async Task<(List<RankItem> top, RankItem? me)> GetLeaderboardAsync(int periodType, int deviceType, int topN, long? userId)
    {
        // 查询当前周期ID
        var now = DateTime.UtcNow;
        var periodId = periodType == 1 ? GetWeekPeriod(now).periodId : GetSeasonPeriod(now).periodId;

        // 先查缓存（Top）
        var topKey = TopCacheKey(periodType, periodId, deviceType, topN);
        if (!_mem.TryGetValue(topKey, out List<RankItem>? top))
        {
            // 分布式缓存
            if (_dist != null)
            {
                var (found, val) = await TryGetDistributedAsync<List<RankItem>>(topKey);
                if (found) top = val;
            }

            if (top is null)
            {
                // DB 查询
                var query = _db.PveRankBoard.AsNoTracking()
                    .Where(x => x.PeriodType == periodType && x.PeriodId == periodId && x.DeviceType == deviceType)
                    .OrderByDescending(x => x.TotalDistanceMeters)
                    .ThenBy(x => x.UpdatedAt);

                var topRows = await query.Take(topN).Select((x) => new { x.UserId, x.TotalDistanceMeters }).ToListAsync();

                top = new List<RankItem>(topRows.Count);
                int r = 1; decimal lastScore = -1; int lastRank = 0;
                foreach (var t in topRows)
                {
                    if (t.TotalDistanceMeters != lastScore)
                    { lastRank = r; lastScore = t.TotalDistanceMeters; }
                    top.Add(new RankItem(t.UserId, t.TotalDistanceMeters, lastRank));
                    r++;
                }

                _mem.Set(topKey, top, TopCacheTtl);
                if (_dist != null) await SetDistributedAsync(topKey, top, TopCacheTtl);
            }
            else
            {
                _mem.Set(topKey, top, TopCacheTtl);
            }
        }

        // '我' 的名次（可选缓存）
        RankItem? me = null;
        if (userId.HasValue)
        {
            var meKey = MeCacheKey(periodType, periodId, deviceType, userId.Value);
            if (!_mem.TryGetValue(meKey, out me))
            {
                if (_dist != null)
                {
                    var (found, val) = await TryGetDistributedAsync<RankItem>(meKey);
                    if (found) me = val;
                }

                if (me is null)
                {
                    var my = await _db.PveRankBoard.AsNoTracking()
                        .Where(x => x.PeriodType == periodType && x.PeriodId == periodId && x.DeviceType == deviceType && x.UserId == userId.Value)
                        .Select(x => new { x.TotalDistanceMeters })
                        .FirstOrDefaultAsync();
                    if (my != null)
                    {
                        var greater = await _db.PveRankBoard.AsNoTracking()
                            .Where(x => x.PeriodType == periodType && x.PeriodId == periodId && x.DeviceType == deviceType && x.TotalDistanceMeters > my.TotalDistanceMeters)
                            .CountAsync();
                        me = new RankItem(userId.Value, my.TotalDistanceMeters, greater + 1);
                        _mem.Set(meKey, me, MeCacheTtl);
                        if (_dist != null) await SetDistributedAsync(meKey, me, MeCacheTtl);
                    }
                }
                else
                {
                    _mem.Set(meKey, me, MeCacheTtl);
                }
            }
        }

        return (top ?? new List<RankItem>(), me);
    }

    public async Task<(bool success, string message, List<(int itemId,int amount)> rewards)> ClaimWeeklyAsync(long userId, int deviceType, DateTime nowUtc)
    {
        var last = GetLastSettledWeek(nowUtc);
        return await ClaimAsync(1, last, deviceType, userId, _cfg.GetWeekRewards(deviceType));
    }

    public async Task<(bool success, string message, List<(int itemId,int amount)> rewards)> ClaimSeasonAsync(long userId, int deviceType, DateTime nowUtc)
    {
        var last = GetCurrentSeasonId(nowUtc); // 赛季按年，随时可领上一赛季，这里简化为当前年实时结算，生产可改为季度
        return await ClaimAsync(2, last, deviceType, userId, _cfg.GetSeasonRewards(deviceType));
    }

    private async Task<(bool success, string message, List<(int itemId,int amount)> rewards)> ClaimAsync(int periodType, int periodId, int deviceType, long userId, List<(int from,int to,List<(int,int)> rewards)> rules)
    {
        // 检查是否已发放
        bool exists = await _db.PveRankRewardGrant.AnyAsync(x => x.PeriodType == periodType && x.PeriodId == periodId && x.DeviceType == deviceType && x.UserId == userId);
        if (exists) return (false, "已领取或已发放", new());

        // 查询玩家名次
        var row = await _db.PveRankBoard.AsNoTracking().FirstOrDefaultAsync(x => x.PeriodType == periodType && x.PeriodId == periodId && x.DeviceType == deviceType && x.UserId == userId);
        if (row == null || row.TotalDistanceMeters <= 0) return (false, "无排名", new());

        var greater = await _db.PveRankBoard.AsNoTracking()
            .Where(x => x.PeriodType == periodType && x.PeriodId == periodId && x.DeviceType == deviceType && x.TotalDistanceMeters > row.TotalDistanceMeters)
            .CountAsync();
        var myRank = greater + 1;

        // 匹配奖励
        var rule = rules.FirstOrDefault(r => myRank >= r.from && myRank <= r.to);
        if (rule.rewards == null || rule.rewards.Count == 0) return (false, "无可领奖励", new());

        // 发放
        foreach (var (itemId, amount) in rule.rewards)
        {
            await _inventory.GrantItemAsync(userId, itemId, amount);
        }

        var grant = new PveRankRewardGrant
        {
            PeriodType = periodType,
            PeriodId = periodId,
            DeviceType = deviceType,
            UserId = userId,
            Rank = myRank,
            RewardJson = System.Text.Json.JsonSerializer.Serialize(rule.rewards),
            CreatedAt = DateTime.UtcNow
        };
        _db.PveRankRewardGrant.Add(grant);
        await _db.SaveChangesAsync();

        return (true, "ok", rule.rewards);
    }

    // Helpers
    public static (int periodType, int periodId) GetWeekPeriod(DateTime dtUtc)
    {
        // ISO week number with Monday as first day
        var cal = CultureInfo.InvariantCulture.Calendar;
        var week = ISOWeek.GetWeekOfYear(dtUtc);
        int year = dtUtc.Year;
        return (1, year * 100 + week); // yyyyWW
    }

    public static (int periodType, int periodId) GetSeasonPeriod(DateTime dtUtc)
    {
        int year = dtUtc.Year;
        return (2, year);
    }

    private int GetCurrentSeasonId(DateTime dtUtc) => GetSeasonPeriod(dtUtc).periodId;

    private int GetLastSettledWeek(DateTime nowUtc)
    {
        // 取配置的结算日（1=Mon..7=Sun）。当周尚未到结算时，上一周为可领取；到达或超过结算时间后，仍按上一周结算。
        int settleDay = _cfg.GetWeeklySettlementDay();
        var local = nowUtc; // 简化：直接使用 UTC
        // 找到本周的该结算日对应的日期
        // 暂未使用结算日配置，直接使用上一周
        // 直接使用 ISO 周的上一周
        var prev = local.AddDays(-7);
        return GetWeekPeriod(prev).periodId;
    }

    // Cache helpers
    private static string TopCacheKey(int periodType, int periodId, int deviceType, int topN)
        => $"rank:top:{periodType}:{periodId}:{deviceType}:{topN}";

    private static string MeCacheKey(int periodType, int periodId, int deviceType, long userId)
        => $"rank:me:{periodType}:{periodId}:{deviceType}:{userId}";

    private async Task<(bool found, T? value)> TryGetDistributedAsync<T>(string key)
    {
        if (_dist is null) return (false, default);
        try
        {
            var bytes = await _dist.GetAsync(key);
            if (bytes is null) return (false, default);
            var val = JsonSerializer.Deserialize<T>(bytes);
            return (val is not null, val);
        }
        catch
        {
            return (false, default);
        }
    }

    private async Task SetDistributedAsync<T>(string key, T value, TimeSpan ttl)
    {
        if (_dist is null) return;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        var opts = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
        await _dist.SetAsync(key, bytes, opts);
    }
}

