using FastEndpoints;
using System.Net.Http.Json;

namespace Travel.Social;

public class FollowStatusRequest
{
    /// <summary>
    /// 单个查询（兼容老客户端）
    /// </summary>
    public long? TargetUserId { get; set; }

    /// <summary>
    /// 批量查询
    /// </summary>
    public List<long>? TargetUserIds { get; set; }
}

public class FollowStatusResponse
{
    public bool Success { get; set; }

    /// <summary>
    /// 单个查询结果（兼容老客户端）
    /// </summary>
    public bool IsFollowed { get; set; }

    /// <summary>
    /// 批量查询结果：key=targetUserId, value=isFollowed
    /// </summary>
    public Dictionary<long, bool>? Status { get; set; }
}

public class EndpointFollowStatus : Endpoint<FollowStatusRequest, FollowStatusResponse>
{
    private readonly HttpClient _client;
    public EndpointFollowStatus(IHttpClientFactory factory) => _client = factory.CreateClient("AppService");

    public override void Configure()
    {
        Post("/travel/follow/status");
        Permissions("web_access");
        Description(b => b.WithSummary("查询关注状态(支持批量)").WithTags("Travel"));
    }

    public override async Task HandleAsync(FollowStatusRequest req, CancellationToken ct)
    {
        // 统一把单个参数映射为列表，便于后续扩展
        if ((req.TargetUserIds == null || req.TargetUserIds.Count == 0) && req.TargetUserId.HasValue)
            req.TargetUserIds = new() { req.TargetUserId.Value };

        var isBatch = req.TargetUserIds is { Count: > 0 } && (req.TargetUserIds.Count > 1 || !req.TargetUserId.HasValue);

        // 约定：如果 APP 服务已支持批量，直接透传；否则回退到本服务循环查询单个接口并聚合
        var res = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(_client, "/social/follow/status", req, ct);

        if (res.IsSuccessStatusCode)
        {
            var data = await res.Content.ReadFromJsonAsync<FollowStatusResponse>(cancellationToken: ct)
                       ?? new FollowStatusResponse { Success = false };
            await HttpContext.Response.SendAsync(data, 200, cancellation: ct);
            return;
        }

        // 回退：仅当批量请求时，尝试逐个查
        if (!isBatch)
        {
            await HttpContext.Response.SendAsync(new FollowStatusResponse { Success = false }, (int)res.StatusCode, cancellation: ct);
            return;
        }

        var dict = new Dictionary<long, bool>(req.TargetUserIds!.Count);
        foreach (var uid in req.TargetUserIds.Distinct())
        {
            var oneRes = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(_client, "/social/follow/status", new { targetUserId = uid }, ct);
            if (!oneRes.IsSuccessStatusCode)
            {
                await HttpContext.Response.SendAsync(new FollowStatusResponse { Success = false }, (int)oneRes.StatusCode, cancellation: ct);
                return;
            }

            var one = await oneRes.Content.ReadFromJsonAsync<FollowStatusResponse>(cancellationToken: ct)
                      ?? new FollowStatusResponse { Success = false };

            if (!one.Success)
            {
                await HttpContext.Response.SendAsync(new FollowStatusResponse { Success = false }, 200, cancellation: ct);
                return;
            }

            dict[uid] = one.IsFollowed;
        }

        await HttpContext.Response.SendAsync(new FollowStatusResponse { Success = true, Status = dict }, 200, cancellation: ct);
    }
}
