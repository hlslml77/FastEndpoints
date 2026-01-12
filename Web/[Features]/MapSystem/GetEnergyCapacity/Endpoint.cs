using FastEndpoints;
using MapSystem.FeedStoredEnergy;
using System.Security.Claims;
using Serilog;
using Web.Services;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace MapSystem.GetEnergyCapacity;

/// <summary>
/// 查询能量槽剩余容量端点（对接 APP 获取各设备可灌输距离）
/// </summary>
public class Endpoint : Endpoint<EnergyCapacityRequest, EnergyCapacityResponse>
{
    private readonly IMapService _mapService;
    private readonly HttpClient _appClient;
    private readonly IConfiguration _cfg;

    private const string AppDeviceDistancePath = "/game/activity/world/user/itemInfo";

    public Endpoint(IMapService mapService, IHttpClientFactory httpClientFactory, IConfiguration cfg)
    {
        _mapService = mapService;
        _appClient = httpClientFactory.CreateClient("AppService");
        _cfg = cfg;
    }

    public override void Configure()
    {
        Post("/map/device-distance");
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("查询设备可灌输距离")
            .WithDescription("调用外部 APP 接口查询能量槽剩余容量并转换为四种设备分别可灌输的最大距离（米）。"));
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

            // 1) 调用 APP 接口获取各设备可灌输距离
            EnergyCapacityResponse? respBody = null;
            try
            {
                using var httpReq = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, AppDeviceDistancePath)
                {
                    Content = JsonContent.Create(new { userId })
                };

                // 转发 Authorization 头（若有需要验证）
                if (HttpContext.Request.Headers.TryGetValue("Authorization", out var authHdr))
                    httpReq.Headers.TryAddWithoutValidation("Authorization", authHdr.ToString());

                // APP side requires appToken in header
                if (req != null && !string.IsNullOrWhiteSpace(req.AppToken))
                    httpReq.Headers.TryAddWithoutValidation("token", req.AppToken);

                var appResp = await _appClient.SendAsync(httpReq, ct);
                if (appResp.IsSuccessStatusCode)
                {
                    var json = await appResp.Content.ReadAsStringAsync(ct);
                    respBody = ParseAppResponse(json);
                }
                else
                {
                    Log.Warning("DeviceDistance - APP 返回非 2xx: {Status}", appResp.StatusCode);
                }
            }
            catch (Exception ex)
            {
                // 捕获任何调用或解析异常，记录后回退到内部计算逻辑
                Log.Error(ex, "调用 APP 获取 device-distance 失败，回退内部逻辑");
            }

            if (respBody == null)
            {
                // fallback：使用内部逻辑计算
                var (remain, dict) = await _mapService.GetFeedCapacityAsync(userId);
                respBody = new EnergyCapacityResponse { RemainingEnergyMeters = remain };
                foreach (var kv in dict)
                    respBody.DeviceDistances.Add(new DeviceDistanceInfo { DeviceType = kv.Key, DistanceMeters = kv.Value });
            }

            await HttpContext.Response.SendAsync(respBody, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetEnergyCapacity failed");
            await SendErrorsAsync(500, "服务器内部错误", ct);
        }
    }

    /// <summary>
    /// 解析 APP 返回数据并转换为 EnergyCapacityResponse。若格式不符合预期，返回 null。
    /// </summary>
    private EnergyCapacityResponse? ParseAppResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            // 允许 "code" 字段为数字或字符串两种格式
            if (!root.TryGetProperty("code", out var codeEl))
                return null;

            int codeVal;
            switch (codeEl.ValueKind)
            {
                case JsonValueKind.Number:
                    codeVal = codeEl.GetInt32();
                    break;
                case JsonValueKind.String when int.TryParse(codeEl.GetString(), out var tmpCode):
                    codeVal = tmpCode;
                    break;
                default:
                    return null;
            }
            if (codeVal != 200)
                return null;

            if (!root.TryGetProperty("data", out var dataEl))
                return null;

            // APP 侧字段示例（按当前 APP 文档）：
            // data: {
            //   "equipmentMainType": 0,
            //   "importRunMileAge": 1166.7
            // }
            // 说明：APP 每次返回一个设备类型的“可灌输最大距离”。

            // 设备主类型映射：0=跑步机, 1/2=单车, 3=划船机
            if (!dataEl.TryGetProperty("equipmentMainType", out var typeEl))
                return null;
            if (!dataEl.TryGetProperty("importRunMileAge", out var distEl))
                return null;

            int equipmentMainType;
            decimal distanceMeters;

            // 兼容字符串与数值两种格式
            switch (typeEl.ValueKind)
            {
                case JsonValueKind.Number:
                    equipmentMainType = typeEl.GetInt32();
                    break;
                case JsonValueKind.String when int.TryParse(typeEl.GetString(), out var tmpInt):
                    equipmentMainType = tmpInt;
                    break;
                default:
                    return null;
            }

            switch (distEl.ValueKind)
            {
                case JsonValueKind.Number:
                    distanceMeters = distEl.GetDecimal();
                    break;
                case JsonValueKind.String when decimal.TryParse(distEl.GetString(), out var tmpDec):
                    distanceMeters = tmpDec;
                    break;
                default:
                    return null;
            }

            var resp = new EnergyCapacityResponse();

            int deviceType = equipmentMainType switch
            {
                0 => 0,
                1 => 2,
                2 => 2,
                3 => 1,
                _ => 3 // 兜底：未知设备按 3（手环/无设备）处理
            };

            resp.DeviceDistances.Add(new DeviceDistanceInfo
            {
                DeviceType = deviceType,
                DistanceMeters = distanceMeters
            });

            // resp 已在上方构建（单设备类型一条记录）

            // 计算剩余能量：取距离 * 效率的最小值（近似），如无数据则为 0
            if (resp.DeviceDistances.Count > 0)
            {
                var eff = new Dictionary<int, decimal> { [0] = 1.2m, [1] = 2.0m, [2] = 1.5m, [3] = 1.0m };
                var min = resp.DeviceDistances
                    .Where(d => eff.ContainsKey(d.DeviceType))
                    .Select(d => d.DistanceMeters * eff[d.DeviceType])
                    .DefaultIfEmpty(0m)
                    .Min();
                resp.RemainingEnergyMeters = Math.Round(min, 3, MidpointRounding.AwayFromZero);
            }
            return resp;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ParseAppResponse error");
            return null;
        }
    }

    private Task SendErrorsAsync(int status, string message, CancellationToken ct)
        => HttpContext.Response.SendAsync(new { statusCode = status, code = Web.Data.ErrorCodes.Common.BadRequest, message }, status, cancellation: ct);
}

