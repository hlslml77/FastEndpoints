using Web.Services;
using FastEndpoints;
using System.Linq;
using System.Security.Claims;
using Serilog;

namespace MapSystem.UnlockWithEnergy;

/// <summary>
/// 使用存储能量解锁终点端点
/// </summary>
public class Endpoint : Endpoint<UnlockWithEnergyRequest, UnlockWithEnergyResponse>
{
    private readonly IMapService _mapService;

    public Endpoint(IMapService mapService)
    {
        _mapService = mapService;
    }

    public override void Configure()
    {
        Post("/map/unlock-with-energy");
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("使用存储能量解锁终点")
            .WithDescription("客户端传起点与终点，消耗玩家存储能量（上限10000米）来解锁终点。需要JWT token验证。"));
    }

    public override async Task HandleAsync(UnlockWithEnergyRequest req, CancellationToken ct)
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

            if (req.StartLocationId <= 0 || req.EndLocationId <= 0)
            {
                var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "起点或终点不合法" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            var (ok, used, remain, unlockedIds, rewardsRaw) = await _mapService.UnlockWithEnergyAsync(userId, req.StartLocationId, req.EndLocationId);

            var resp = new UnlockWithEnergyResponse
            {
                IsUnlocked = ok,
                UsedEnergyMeters = used,
                StoredEnergyMeters = remain,
                UnlockedLocationIds = unlockedIds ?? new List<int>(),
                Rewards = rewardsRaw?.Select(r => new RewardItem { ItemId = r[0], Amount = r[1] }).ToList() ?? new List<RewardItem>()
            };

            await HttpContext.Response.SendAsync(resp, 200, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "UnlockWithEnergy argument error. start={Start}, end={End}", req.StartLocationId, req.EndLocationId);
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = ex.Message };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "UnlockWithEnergy failed");
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

