using FastEndpoints;
using RankApi;
using Web.Services;
using System.Security.Claims;

namespace RankApi.GetLeaderboard;

public class Endpoint : Endpoint<LeaderboardRequest, LeaderboardResponse>
{
    private readonly IPveRankService _rankService;

    public Endpoint(IPveRankService rankService)
    {
        _rankService = rankService;
    }

    public override void Configure()
    {
        Post("rank/leaderboard");
        Permissions("web_access");
        Description(x => x.WithTags("Rank").WithSummary("获取排行榜").WithDescription("periodType: 1=周榜,2=赛季榜; deviceType: 0=跑步,1=划船,2=单车,3=手环; Top 默认 100").Produces<LeaderboardResponse>(200, "application/json"));
    }

    public override async Task HandleAsync(LeaderboardRequest req, CancellationToken ct)
    {
        long? userId = null;
        var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var uid)) userId = uid;

        var (items, me) = await _rankService.GetLeaderboardAsync(req.PeriodType, req.DeviceType, Math.Clamp(req.Top, 1, 100), userId);

        int periodId = req.PeriodType == 1
            ? Web.Services.PveRankService.GetWeekPeriod(DateTime.UtcNow).periodId
            : Web.Services.PveRankService.GetSeasonPeriod(DateTime.UtcNow).periodId;

        var res = new LeaderboardResponse
        {
            PeriodType = req.PeriodType,
            DeviceType = req.DeviceType,
            PeriodId = periodId,
            Top = items.Select(i => new LeaderboardItemDto { UserId = i.UserId, DistanceMeters = i.TotalDistanceMeters, Rank = i.Rank }).ToList(),
            Me = me is null ? null : new LeaderboardItemDto { UserId = me.UserId, DistanceMeters = me.TotalDistanceMeters, Rank = me.Rank }
        };

        await HttpContext.Response.SendOkAsync(res, cancellation: ct);
    }
}

