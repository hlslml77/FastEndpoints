using FastEndpoints;
using Web.Services;
using System.Security.Claims;
using Serilog;

namespace MapSystem.GetPlayerMapState;

/// <summary>
/// 获取玩家地图状态（已解锁点位、已完成点位、所有进度）
/// </summary>
public class Endpoint : Endpoint<EmptyRequest, GetPlayerMapStateResponse>
{
    private readonly IMapService _mapService;

    public Endpoint(IMapService mapService)
    {
        _mapService = mapService;
    }

    public override void Configure()
    {
        Post("/map/player-state");
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("获取玩家地图状态")
            .WithDescription("返回玩家已解锁的点位、已完成的点位以及所有路线进度信息。解锁点位通过save-progress接口在距离超过配置时自动解锁。"));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
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

            var unlocked = await _mapService.GetPlayerUnlockedLocationsAsync(userId);
            var completed = await _mapService.GetPlayerCompletedLocationsAsync(userId);
            var progress = await _mapService.GetPlayerProgressAsync(userId);

            var resp = new GetPlayerMapStateResponse
            {
                UnlockedLocationIds = unlocked.Select(u => u.LocationId).ToList(),
                CompletedLocationIds = completed.Select(c => c.LocationId).ToList(),
                ProgressRecords = progress.Select(p => new PlayerProgressDto
                {
                    StartLocationId = p.StartLocationId,
                    EndLocationId = p.EndLocationId,
                    DistanceMeters = p.DistanceMeters,
                    CreatedAt = p.CreatedAt
                }).ToList()
            };

            await HttpContext.Response.SendAsync(resp, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetPlayerMapState failed");
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}
