using FastEndpoints;
using Serilog;
using System.Security.Claims;
using Web.Services;

namespace MapSystem.GetLocationPeopleCount;

/// <summary>
/// 查询点位信息（人数与下次挑战时间）
/// </summary>
public class Endpoint : Endpoint<GetLocationPeopleCountRequest, GetLocationPeopleCountResponse>
{
    private readonly IMapService _mapService;

    public Endpoint(IMapService mapService)
    {
        _mapService = mapService;
    }

    public override void Configure()
    {
        Post("/map/location-info");
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("查询点位信息")
            .WithDescription("返回指定点位的当前人数统计与玩家的下次挑战时间。当玩家调用 /api/map/visit-location 或 /api/map/save-progress 接口时，该点位的人数会自动增加。若统计人数为0，或统计为1且只有当前玩家自己，则按 Config.json 中的玩家选择大地图点位时机器人数量显示的 Value4 区间生成随机展示人数"));
    }

    public override async Task HandleAsync(GetLocationPeopleCountRequest req, CancellationToken ct)
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

            var (count, nextChallengeTime) = await _mapService.CountPlayersAtLocationAsync(userId, req.LocationId);
            await HttpContext.Response.SendAsync(new GetLocationPeopleCountResponse
            {
                PeopleCount = count,
                NextChallengeTime = nextChallengeTime
            }, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetLocationInfo failed. locationId={LocationId}", req.LocationId);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

