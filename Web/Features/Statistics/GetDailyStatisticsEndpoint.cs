using FastEndpoints;
using Web.Services;

namespace Web.Features.Statistics;

/// <summary>
/// 获取每日游戏统计数据请求
/// </summary>
public class GetDailyStatisticsRequest
{
    /// <summary>
    /// 统计日期 (格式: yyyy-MM-dd)
    /// </summary>
    public string? Date { get; set; }

    /// <summary>
    /// 最近N天的数据 (如果不指定Date，则返回最近N天的数据)
    /// </summary>
    public int Days { get; set; } = 7;
}

/// <summary>
/// 每日游戏统计数据响应
/// </summary>
public class DailyStatisticsResponse
{
    /// <summary>
    /// 统计日期
    /// </summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>
    /// 当天新注册玩家数
    /// </summary>
    public int NewRegistrations { get; set; }

    /// <summary>
    /// 当天活跃玩家数
    /// </summary>
    public int ActivePlayers { get; set; }

    /// <summary>
    /// 最大在线人数
    /// </summary>
    public int MaxOnlinePlayers { get; set; }

    /// <summary>
    /// 平均在线人数
    /// </summary>
    public decimal AvgOnlinePlayers { get; set; }

    /// <summary>
    /// 总玩家数
    /// </summary>
    public int TotalPlayers { get; set; }
}

/// <summary>
/// 获取每日游戏统计数据端点
/// </summary>
public class GetDailyStatisticsEndpoint : EndpointWithoutRequest<object>
{
    private readonly IGameStatisticsService _statisticsService;

    public GetDailyStatisticsEndpoint(IGameStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    public override void Configure()
    {
        Get("/api/admin/statistics/daily");
        AllowAnonymous();
        Tags("Admin", "Statistics");
        Description(b => b
            .WithName("Get Daily Game Statistics")
            .WithDescription("获取每日游戏统计数据"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var dateParam = HttpContext.Request.Query["date"].ToString();
        var daysParam = HttpContext.Request.Query["days"].ToString();

        if (string.IsNullOrWhiteSpace(dateParam))
        {
            // 返回最近N天的数据
            var days = int.TryParse(daysParam, out var d) ? d : 7;
            var statistics = await _statisticsService.GetRecentStatisticsAsync(days);

            var response = statistics.Select(s => new DailyStatisticsResponse
            {
                Date = s.StatisticsDate.ToString("yyyy-MM-dd"),
                NewRegistrations = s.NewRegistrations,
                ActivePlayers = s.ActivePlayers,
                MaxOnlinePlayers = s.MaxOnlinePlayers,
                AvgOnlinePlayers = s.AvgOnlinePlayers,
                TotalPlayers = s.TotalPlayers
            }).ToList();

            await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
        }
        else
        {
            // 返回指定日期的数据
            if (DateOnly.TryParse(dateParam, out var date))
            {
                var statistics = await _statisticsService.GetDailyStatisticsAsync(date);
                if (statistics == null)
                {
                    await HttpContext.Response.SendAsync(new { statusCode = 404, message = "not found" }, 404, cancellation: ct);
                    return;
                }

                var response = new DailyStatisticsResponse
                {
                    Date = statistics.StatisticsDate.ToString("yyyy-MM-dd"),
                    NewRegistrations = statistics.NewRegistrations,
                    ActivePlayers = statistics.ActivePlayers,
                    MaxOnlinePlayers = statistics.MaxOnlinePlayers,
                    AvgOnlinePlayers = statistics.AvgOnlinePlayers,
                    TotalPlayers = statistics.TotalPlayers
                };

                await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
            }
            else
            {
                await HttpContext.Response.SendAsync(new { statusCode = 400, message = "bad request" }, 400, cancellation: ct);
            }
        }
    }
}

