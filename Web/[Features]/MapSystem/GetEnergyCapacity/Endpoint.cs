using FastEndpoints;
using MapSystem.FeedStoredEnergy;
using System.Security.Claims;
using Serilog;
using Web.Services;

namespace MapSystem.GetEnergyCapacity;

/// <summary>
/// 查询能量槽剩余容量端点
/// </summary>
public class Endpoint : Endpoint<EnergyCapacityRequest, EnergyCapacityResponse>
{
    private readonly IMapService _mapService;

    public Endpoint(IMapService mapService)
    {
        _mapService = mapService;
    }

    public override void Configure()
    {
        Post("/map/device-distance");
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("查询设备可灌输距离")
            .WithDescription("返回四种设备各自最多还能灌输的距离（米）。"));
    }

    public override async Task HandleAsync(EnergyCapacityRequest req, CancellationToken ct)
    {
        try
        {
            long userId;
            if (req?.UserId is > 0)
            {
                userId = req.UserId.Value;
            }
            else
            {
                var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out userId))
                {
                    await SendErrorsAsync(400, "未能从令牌解析用户ID", ct);
                    return;
                }
            }

            var (remain, dict) = await _mapService.GetFeedCapacityAsync(userId);
            var resp = new EnergyCapacityResponse { RemainingEnergyMeters = remain };
            foreach (var kv in dict)
            {
                resp.DeviceDistances.Add(new DeviceDistanceInfo { DeviceType = kv.Key, DistanceMeters = kv.Value });
            }
            await Send.OkAsync(resp, ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetEnergyCapacity failed");
            await SendErrorsAsync(500, "服务器内部错误", ct);
        }
    }

    private Task SendErrorsAsync(int status, string message, CancellationToken ct)
        => HttpContext.Response.SendAsync(new { statusCode = status, code = Web.Data.ErrorCodes.Common.BadRequest, message }, status, cancellation: ct);
}

