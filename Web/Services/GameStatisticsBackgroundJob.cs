using Microsoft.EntityFrameworkCore;
using Serilog;
using Web.Data;
using Web.Data.Entities;

namespace Web.Services;

/// <summary>
/// 游戏统计后台任务
/// 定期收集和统计游戏数据
/// </summary>
public class GameStatisticsBackgroundJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GameStatisticsBackgroundJob> _logger;
    private readonly TimeSpan _startupDelay = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _hourlyInterval = TimeSpan.FromHours(1);

    public GameStatisticsBackgroundJob(IServiceProvider serviceProvider, ILogger<GameStatisticsBackgroundJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Game Statistics Background Job started");

        try
        {
            // 启动时先延迟一段时间，避免刚启动就执行统计
            await Task.Delay(_startupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // 每小时记录一次在线人数
                    await RecordHourlyOnlineCountAsync();

                    // 每天午夜生成前一天的统计数据
                    await CheckAndGenerateDailyStatisticsAsync();

                    // 每天更新玩家活动统计
                    await UpdatePlayerActivityStatisticsAsync();

                    await Task.Delay(_hourlyInterval, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in game statistics background job");
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Game Statistics Background Job cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Game Statistics Background Job failed");
        }
    }

    private async Task RecordHourlyOnlineCountAsync()
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var statisticsService = scope.ServiceProvider.GetRequiredService<IGameStatisticsService>();

        try
        {
            // 获取当前在线玩家数（这里简化处理，实际应该从会话管理器获取）
            // 这里我们统计最近5分钟内有活动的玩家
            var fiveMinutesAgo = DateTime.UtcNow.AddMinutes(-5);
            var onlineCount = await dbContext.PlayerRole
                .Where(p => p.LastUpdateTime >= fiveMinutesAgo)
                .CountAsync();

            await statisticsService.RecordOnlinePlayersSnapshotAsync(onlineCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record hourly online count");
        }
    }

    private async Task CheckAndGenerateDailyStatisticsAsync()
    {
        var now = DateTime.UtcNow;
        // 在每天的 00:05 生成前一天的统计
        if (now.Hour == 0 && now.Minute >= 5 && now.Minute < 6)
        {
            var yesterday = DateOnly.FromDateTime(now.AddDays(-1));
            await GenerateDailyStatisticsAsync(yesterday);
        }
    }

    private async Task GenerateDailyStatisticsAsync(DateOnly date)
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var statisticsService = scope.ServiceProvider.GetRequiredService<IGameStatisticsService>();

        try
        {
            await statisticsService.GenerateDailyStatisticsAsync(date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily statistics for {Date}", date);
        }
    }

    private async Task GenerateYesterdayStatisticsAsync()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await GenerateDailyStatisticsAsync(yesterday);
    }

    private async Task UpdatePlayerActivityStatisticsAsync()
    {
        using var scope = _serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var startOfDay = today.ToDateTime(TimeOnly.MinValue);
            var endOfDay = today.ToDateTime(TimeOnly.MaxValue);

            // 计算今天完成的地图点位总数
            var locationsCompleted = await dbContext.PlayerCompletedLocation
                .Where(l => l.CompletedTime >= startOfDay && l.CompletedTime <= endOfDay)
                .CountAsync();

            // 计算今天完成的事件总数
            var eventsCompleted = await dbContext.PlayerDailyRandomEvent
                .Where(e => e.CompletedAt >= startOfDay && e.CompletedAt <= endOfDay && e.IsCompleted)
                .CountAsync();

            // 计算今天的总跑步距离
            var totalDistance = await dbContext.PlayerMapProgress
                .Where(p => p.CreatedAt >= startOfDay && p.CreatedAt <= endOfDay)
                .SumAsync(p => (decimal?)p.DistanceMeters) ?? 0;

            // 计算平均玩家等级
            var avgLevel = await dbContext.PlayerRole
                .AverageAsync(p => (decimal?)p.CurrentLevel) ?? 0;

            var activityStats = new PlayerActivityStatistics
            {
                StatisticsDate = today,
                TotalLocationsCompleted = locationsCompleted,
                TotalEventsCompleted = eventsCompleted,
                TotalDistanceMeters = totalDistance,
                AvgPlayerLevel = avgLevel,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var existing = await dbContext.PlayerActivityStatistics
                .FirstOrDefaultAsync(s => s.StatisticsDate == today);

            if (existing != null)
            {
                existing.TotalLocationsCompleted = locationsCompleted;
                existing.TotalEventsCompleted = eventsCompleted;
                existing.TotalDistanceMeters = totalDistance;
                existing.AvgPlayerLevel = avgLevel;
                existing.UpdatedAt = DateTime.UtcNow;
                dbContext.PlayerActivityStatistics.Update(existing);
            }
            else
            {
                dbContext.PlayerActivityStatistics.Add(activityStats);
            }

            await dbContext.SaveChangesAsync();
            _logger.LogDebug("Updated player activity statistics for {Date}", today);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update player activity statistics");
        }
    }
}

