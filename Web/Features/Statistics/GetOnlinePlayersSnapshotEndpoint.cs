using FastEndpoints;
using Web.Services;

namespace Web.Features.Statistics;

/// <summary>
/// 在线人数快照响应
/// </summary>
public class OnlinePlayersSnapshotResponse
{
    /// <summary>
    /// 小时 (0-23)
    /// </summary>
    public int Hour { get; set; }

    /// <summary>
    /// 该小时的在线人数
    /// </summary>
    public int OnlineCount { get; set; }

    /// <summary>
    /// 记录时间
    /// </summary>
    public string RecordedAt { get; set; } = string.Empty;
}

/// <summary>
/// 获取在线人数快照端点
/// </summary>
public class GetOnlinePlayersSnapshotEndpoint : EndpointWithoutRequest<object>
{
    private readonly IGameStatisticsService _statisticsService;

    public GetOnlinePlayersSnapshotEndpoint(IGameStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    public override void Configure()
    {
        Get("/api/admin/statistics/online-snapshots");
        AllowAnonymous();
        Tags("Admin", "Statistics");
        Description(b => b
            .WithName("Get Online Players Snapshots")
            .WithDescription("获取在线人数快照数据"));
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
            var snapshots = await _statisticsService.GetOnlineSnapshotsAsync(date);

            var response = snapshots.Select(s => new OnlinePlayersSnapshotResponse
            {
                Hour = s.Hour,
                OnlineCount = s.OnlineCount,
                RecordedAt = s.RecordedAt.ToString("yyyy-MM-dd HH:mm:ss")
            }).ToList();

            await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
        }
        else
        {
            await HttpContext.Response.SendAsync(new { statusCode = 400, message = "bad request" }, 400, cancellation: ct);
        }
    }
}

