using FastEndpoints;
using System.Net.Http.Json;

namespace Travel.Social;

public class FollowStatusRequest { public long TargetUserId { get; set; } }

public class FollowStatusResponse { public bool Success { get; set; } public bool IsFollowed { get; set; } }

public class EndpointFollowStatus : Endpoint<FollowStatusRequest, FollowStatusResponse>
{
    private readonly HttpClient _client;
    public EndpointFollowStatus(IHttpClientFactory factory) => _client = factory.CreateClient("AppService");

    public override void Configure()
    {
        Post("/travel/follow/status");
        Permissions("web_access");
        Description(b => b.WithSummary("查询关注状态 (由 APP 服务返回)").WithTags("Travel"));
    }

    public override async Task HandleAsync(FollowStatusRequest req, CancellationToken ct)
    {
        var res = await _client.PostAsJsonAsync("/social/follow/status", req, ct);
        if (!res.IsSuccessStatusCode)
        {
            await HttpContext.Response.SendAsync(new FollowStatusResponse { Success = false }, (int)res.StatusCode, cancellation: ct);
            return;
        }
        var data = await res.Content.ReadFromJsonAsync<FollowStatusResponse>(cancellationToken: ct) ?? new FollowStatusResponse { Success = false };
        await HttpContext.Response.SendAsync(data, 200, cancellation: ct);
    }
}
