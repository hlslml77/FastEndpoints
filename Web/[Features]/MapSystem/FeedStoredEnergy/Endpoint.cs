using FastEndpoints;
using MapSystem.FeedStoredEnergy;
using System.Security.Claims;
using Serilog;
using Web.Services;

namespace MapSystem.FeedStoredEnergy;

/// <summary>
/// 主动灌输能量端点
/// </summary>
public class Endpoint : Endpoint<FeedEnergyRequest, FeedEnergyResponse>
{
    private readonly IMapService _mapService;

    public Endpoint(IMapService mapService)
    {
        _mapService = mapService;
    }

    public override void Configure()
    {
        Post("/map/feed-energy");
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("主动灌输能量条")
            .WithDescription("客户端上传设备类型与距离，将距离按设备效率转为存储能量并累加到玩家能量槽（上限10000m）。"));
    }

    public override async Task HandleAsync(FeedEnergyRequest req, CancellationToken ct)
    {
        try
        {
            var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
            {
                await SendErrorsAsync(400, "未能从令牌解析用户ID", ct);
                return;
            }

            var (used, stored) = await _mapService.FeedStoredEnergyAsync(userId, req.DeviceType, req.DistanceMeters);
            await Send.OkAsync(new FeedEnergyResponse { UsedDistanceMeters = used, StoredEnergyMeters = stored }, ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "FeedEnergy argument error");
            await SendErrorsAsync(400, ex.Message, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FeedEnergy failed");
            await SendErrorsAsync(500, "服务器内部错误", ct);
        }
    }

    private Task SendErrorsAsync(int status, string message, CancellationToken ct)
        => HttpContext.Response.SendAsync(new { statusCode = status, code = Web.Data.ErrorCodes.Common.BadRequest, message }, status, cancellation: ct);
}

