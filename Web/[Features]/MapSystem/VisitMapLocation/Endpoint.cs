using Web.Services;
using FastEndpoints;

namespace MapSystem.VisitMapLocation;

/// <summary>
/// 访问地图点位端点
/// </summary>
public class Endpoint : Endpoint<VisitMapLocationRequest, VisitMapLocationResponse>
{
    private readonly IMapService _mapService;

    public Endpoint(IMapService mapService)
    {
        _mapService = mapService;
    }

    public override void Configure()
    {
        Post("/map/visit-location");
        AllowAnonymous();
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("访问地图点位")
            .WithDescription("记录玩家访问地图点位，首次访问发放首次奖励，非首次访问发放固定奖励"));
    }

    public override async Task HandleAsync(VisitMapLocationRequest req, CancellationToken ct)
    {
        try
        {
            var result = await _mapService.VisitMapLocationAsync(req.UserId, req.LocationId);

            // 转换奖励格式
            List<RewardItem>? rewards = null;
            if (result.Rewards != null && result.Rewards.Count > 0)
            {
                rewards = result.Rewards.Select(r => new RewardItem
                {
                    ItemId = r[0],
                    Amount = r[1]
                }).ToList();
            }

            var response = new VisitMapLocationResponse
            {
                IsFirstVisit = result.IsFirstVisit,
                Rewards = rewards,
                VisitCount = result.VisitRecord?.VisitCount ?? 0,
                FirstVisitTime = result.VisitRecord?.FirstVisitTime ?? DateTime.UtcNow,
                LastVisitTime = result.VisitRecord?.LastVisitTime ?? DateTime.UtcNow,
                LocationInfo = result.MapConfig != null ? new MapLocationInfo
                {
                    LocationId = result.MapConfig.LocationID,
                    Description = result.MapConfig.Des,
                    ScenicSpot = result.MapConfig.ScenicSpot,
                    Hierarchy = result.MapConfig.Hierarchy
                } : null
            };

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
        catch (Exception ex)
        {
            ThrowError($"访问地图点位失败: {ex.Message}");
        }
    }
}

