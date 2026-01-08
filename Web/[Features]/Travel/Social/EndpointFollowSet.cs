using FastEndpoints;
using System.Net.Http.Json;

namespace Travel.Social;

public class FollowSetRequest
{
    public long TargetUserId { get; set; }
    public bool Follow { get; set; }
}

public class FollowSetResponse
{
    public bool Success { get; set; }
    public bool IsFollowed { get; set; }
}

public class EndpointFollowSet : Endpoint<FollowSetRequest, FollowSetResponse>
{
    private readonly HttpClient _client;
    public EndpointFollowSet(IHttpClientFactory factory) => _client = factory.CreateClient("AppService");

    public override void Configure()
    {
        Post("/travel/follow/set");
        Permissions("web_access");
        Description(b => b.WithSummary("设置关注状态 (由 APP 服务处理)").WithTags("Travel"));
    }

    public override async Task HandleAsync(FollowSetRequest req, CancellationToken ct)
    {
        var res = await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(_client, "/social/follow/set", req, ct);
        if (!res.IsSuccessStatusCode)
        {
            await HttpContext.Response.SendAsync(new FollowSetResponse { Success = false }, (int)res.StatusCode, cancellation: ct);
            return;
        }
        var data = await res.Content.ReadFromJsonAsync<FollowSetResponse>(cancellationToken: ct) ?? new FollowSetResponse { Success = false };
        await HttpContext.Response.SendAsync(data, 200, cancellation: ct);
    }
}
