using Web.Services;
using FastEndpoints;
using System.Security.Claims;
using Serilog;

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
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("访问地图点位")
            .WithDescription("记录玩家访问地图点位。客户端上报是否完成：首次访问发放首次奖励；完成则发放完成奖励（使用固定奖励字段）。注意：点位解锁通过/map/save-progress接口实现，此接口不再添加到解锁列表。需要JWT token验证。"));
    }

    public override async Task HandleAsync(VisitMapLocationRequest req, CancellationToken ct)
    {
        try
        {
            var userIdStr = User?.Claims?.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
            {
                var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            var result = await _mapService.VisitMapLocationAsync(userId, req.LocationId, req.IsCompleted);

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
                DidConsumeItem = result.DidConsumeItem,
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

            await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("物品不足"))
        {
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.Unprocessable, message = ex.Message };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "VisitMapLocation argument error. locationId={LocationId}, isCompleted={IsCompleted}", req.LocationId, req.IsCompleted);
            var isNotFound = ex.Message?.IndexOf("Location", StringComparison.OrdinalIgnoreCase) >= 0;
            var errorBody = isNotFound
                ? new { statusCode = 404, code = Web.Data.ErrorCodes.Map.LocationNotFound, message = "地点不存在" }
                : new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = ex.Message ?? "请求参数错误" };
            await HttpContext.Response.SendAsync(errorBody, errorBody.statusCode, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "VisitMapLocation failed");
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}
