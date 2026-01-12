using FastEndpoints;
using MapSystem.FeedStoredEnergy;
using System.Security.Claims;
using Serilog;
using Web.Services;
using System.Net.Http;
using System.Net.Http.Json;

namespace MapSystem.FeedStoredEnergy;

/// <summary>
/// 主动灌输能量端点
/// </summary>
public class Endpoint : Endpoint<FeedEnergyRequest, FeedEnergyResponse>
{
    private readonly IMapService _mapService;
    private readonly HttpClient _appClient;
    private readonly IConfiguration _cfg;

    private const string AppFeedEnergyPath = "/game/activity/world/upload";

    public Endpoint(IMapService mapService, IHttpClientFactory httpClientFactory, IConfiguration cfg)
    {
        _mapService = mapService;
        _appClient = httpClientFactory.CreateClient("AppService");
        _cfg = cfg;
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
            // 1) continue with server-side processing
            var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
            {
                await SendErrorsAsync(400, "未能从令牌解析用户ID", ct);
                return;
            }

            // 2) forward request to external APP service first
            using (var appHttpReq = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, AppFeedEnergyPath))
            {
                appHttpReq.Content = JsonContent.Create(new { deviceType = req.DeviceType, distanceMeters = req.DistanceMeters });

                // attach userid in header as required
                appHttpReq.Headers.TryAddWithoutValidation("userid", userId.ToString());

                // keep forwarding web Authorization too (if APP also checks it)
                if (HttpContext.Request.Headers.TryGetValue("Authorization", out var authHdr))
                    appHttpReq.Headers.TryAddWithoutValidation("Authorization", authHdr.ToString());

                var appResp = await _appClient.SendAsync(appHttpReq, ct);
                if (!appResp.IsSuccessStatusCode)
                {
                    var msg = $"APP 服务响应 {appResp.StatusCode}";
                    Log.Warning("FeedEnergy - call APP failed: {Status}", appResp.StatusCode);
                    await SendErrorsAsync((int)appResp.StatusCode, msg, ct);
                    return;
                }
            }

            var (used, stored) = await _mapService.FeedStoredEnergyAsync(userId, req.DeviceType, req.DistanceMeters);
            await HttpContext.Response.SendAsync(new FeedEnergyResponse { UsedDistanceMeters = used, StoredEnergyMeters = stored }, 200, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "FeedEnergy argument error");
            await SendErrorsAsync(400, ex.Message, ct);
        }
        catch (HttpRequestException ex)
        {
            Log.Error(ex, "FeedEnergy - HTTP call failed");
            await SendErrorsAsync(502, "网关错误", ct);
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

