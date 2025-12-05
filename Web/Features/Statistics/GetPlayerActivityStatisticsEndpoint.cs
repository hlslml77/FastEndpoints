using FastEndpoints;
using Web.Services;

namespace Web.Features.Statistics;

/// <summary>
/// 玩家活动统计响应
/// </summary>
public class PlayerActivityStatisticsResponse
{
    /// <summary>
    /// 统计日期
    /// </summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>
    /// 完成的地图点位总数
    /// </summary>
    public int TotalLocationsCompleted { get; set; }

    /// <summary>
    /// 完成的旅行事件总数
    /// </summary>
    public int TotalEventsCompleted { get; set; }

    /// <summary>
    /// 总跑步距离（米）
    /// </summary>
    public decimal TotalDistanceMeters { get; set; }

    /// <summary>
    /// 平均玩家等级
    /// </summary>
    public decimal AvgPlayerLevel { get; set; }
}

/// <summary>
/// 获取玩家活动统计端点
/// </summary>
public class GetPlayerActivityStatisticsEndpoint : EndpointWithoutRequest<object>
{
    private readonly IGameStatisticsService _statisticsService;

    public GetPlayerActivityStatisticsEndpoint(IGameStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    public override void Configure()
    {
        Get("/api/admin/statistics/player-activity");
        AllowAnonymous();
        Tags("Admin", "Statistics");
        Description(b => b
            .WithName("Get Player Activity Statistics")
            .WithDescription("获取玩家活动统计数据"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var dateParam = HttpContext.Request.Query["date"].ToString();

        if (string.IsNullOrWhiteSpace(dateParam))
        {
            // 默认返回今天的数据
            dateParam = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");
        }

        if (DateOnly.TryParse(dateParam, out var date))
        {
            var statistics = await _statisticsService.GetPlayerActivityStatisticsAsync(date);

            if (statistics == null)
            {
                await HttpContext.Response.SendAsync(new { statusCode = 404, message = "not found" }, 404, cancellation: ct);
                return;
            }

            var response = new PlayerActivityStatisticsResponse
            {
                Date = statistics.StatisticsDate.ToString("yyyy-MM-dd"),
                TotalLocationsCompleted = statistics.TotalLocationsCompleted,
                TotalEventsCompleted = statistics.TotalEventsCompleted,
                TotalDistanceMeters = statistics.TotalDistanceMeters,
                AvgPlayerLevel = statistics.AvgPlayerLevel
            };

            await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
        }
        else
        {
            await HttpContext.Response.SendAsync(new { statusCode = 400, message = "bad request" }, 400, cancellation: ct);
        }
    }
}

