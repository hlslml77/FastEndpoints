using Microsoft.EntityFrameworkCore;
using Serilog;
using Web.Data;
using Web.Data.Entities;

namespace Web.Services;

/// <summary>
/// 游戏统计服务接口
/// </summary>
public interface IGameStatisticsService
{
    /// <summary>
    /// 记录在线人数快照
    /// </summary>
    Task RecordOnlinePlayersSnapshotAsync(int onlineCount);

    /// <summary>
    /// 生成每日统计数据
    /// </summary>
    Task GenerateDailyStatisticsAsync(DateOnly date);

    /// <summary>
    /// 获取指定日期的统计数据
    /// </summary>
    Task<DailyGameStatistics?> GetDailyStatisticsAsync(DateOnly date);

    /// <summary>
    /// 获取最近N天的统计数据
    /// </summary>
    Task<List<DailyGameStatistics>> GetRecentStatisticsAsync(int days);

    /// <summary>
    /// 获取指定日期的在线人数快照
    /// </summary>
    Task<List<OnlinePlayersSnapshot>> GetOnlineSnapshotsAsync(DateOnly date);

    /// <summary>
    /// 获取玩家活动统计
    /// </summary>
    Task<PlayerActivityStatistics?> GetPlayerActivityStatisticsAsync(DateOnly date);
}

public class GameStatisticsService : IGameStatisticsService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<GameStatisticsService> _logger;

    public GameStatisticsService(AppDbContext dbContext, ILogger<GameStatisticsService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task RecordOnlinePlayersSnapshotAsync(int onlineCount)
    {
        try
        {
            var now = DateTime.UtcNow;
            var snapshot = new OnlinePlayersSnapshot
            {
                StatisticsDate = DateOnly.FromDateTime(now),
                Hour = now.Hour,
                OnlineCount = onlineCount,
                RecordedAt = now
            };

            _dbContext.OnlinePlayersSnapshot.Add(snapshot);
            await _dbContext.SaveChangesAsync();
            _logger.LogDebug("Recorded online players snapshot: {OnlineCount} at {Time}", onlineCount, now);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record online players snapshot");
        }
    }

    public async Task GenerateDailyStatisticsAsync(DateOnly date)
    {
        try
        {
            var startOfDay = date.ToDateTime(TimeOnly.MinValue);
            var endOfDay = date.ToDateTime(TimeOnly.MaxValue);

            // 获取新注册玩家数
            var newRegistrations = await _dbContext.PlayerRole
                .Where(p => p.LastUpdateTime >= startOfDay && p.LastUpdateTime <= endOfDay)
                .CountAsync();

            // 获取活跃玩家数
            var activePlayers = await _dbContext.PlayerRole
                .Where(p => p.LastUpdateTime >= startOfDay && p.LastUpdateTime <= endOfDay)
                .CountAsync();

            // 获取最大在线人数
            var maxOnline = await _dbContext.OnlinePlayersSnapshot
                .Where(s => s.StatisticsDate == date)
                .MaxAsync(s => (int?)s.OnlineCount) ?? 0;

            // 获取平均在线人数
            var avgOnline = await _dbContext.OnlinePlayersSnapshot
                .Where(s => s.StatisticsDate == date)
                .AverageAsync(s => (decimal?)s.OnlineCount) ?? 0;

            // 获取总玩家数
            var totalPlayers = await _dbContext.PlayerRole.CountAsync();

            var statistics = new DailyGameStatistics
            {
                StatisticsDate = date,
                NewRegistrations = newRegistrations,
                ActivePlayers = activePlayers,
                MaxOnlinePlayers = maxOnline,
                AvgOnlinePlayers = avgOnline,
                TotalPlayers = totalPlayers,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // 检查是否已存在该日期的统计
            var existing = await _dbContext.DailyGameStatistics
                .FirstOrDefaultAsync(s => s.StatisticsDate == date);

            if (existing != null)
            {
                existing.NewRegistrations = statistics.NewRegistrations;
                existing.ActivePlayers = statistics.ActivePlayers;
                existing.MaxOnlinePlayers = statistics.MaxOnlinePlayers;
                existing.AvgOnlinePlayers = statistics.AvgOnlinePlayers;
                existing.TotalPlayers = statistics.TotalPlayers;
                existing.UpdatedAt = DateTime.UtcNow;
                _dbContext.DailyGameStatistics.Update(existing);
            }
            else
            {
                _dbContext.DailyGameStatistics.Add(statistics);
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogDebug("Generated daily statistics for {Date}: New={New}, Active={Active}, MaxOnline={Max}, AvgOnline={Avg}",
                date, newRegistrations, activePlayers, maxOnline, avgOnline);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily statistics for {Date}", date);
        }
    }

    public async Task<DailyGameStatistics?> GetDailyStatisticsAsync(DateOnly date)
    {
        return await _dbContext.DailyGameStatistics
            .FirstOrDefaultAsync(s => s.StatisticsDate == date);
    }

    public async Task<List<DailyGameStatistics>> GetRecentStatisticsAsync(int days)
    {
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        return await _dbContext.DailyGameStatistics
            .Where(s => s.StatisticsDate >= startDate)
            .OrderByDescending(s => s.StatisticsDate)
            .ToListAsync();
    }

    public async Task<List<OnlinePlayersSnapshot>> GetOnlineSnapshotsAsync(DateOnly date)
    {
        return await _dbContext.OnlinePlayersSnapshot
            .Where(s => s.StatisticsDate == date)
            .OrderBy(s => s.Hour)
            .ToListAsync();
    }

    public async Task<PlayerActivityStatistics?> GetPlayerActivityStatisticsAsync(DateOnly date)
    {
        return await _dbContext.PlayerActivityStatistics
            .FirstOrDefaultAsync(s => s.StatisticsDate == date);
    }
}

