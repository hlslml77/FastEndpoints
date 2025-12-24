using FastEndpoints;
using System.Net.Http.Json;

namespace Travel.Social;

public class RandomPlayerResponse
{
    public bool Success { get; set; }
    public object? UserInfo { get; set; }
    public bool IsFollowed { get; set; }
    public int EncounterCount { get; set; }
}

public class EndpointRandomPlayer : EndpointWithoutRequest<RandomPlayerResponse>
{
    private readonly HttpClient _client;
    public EndpointRandomPlayer(IHttpClientFactory factory) => _client = factory.CreateClient("AppService");

    public override void Configure()
    {
        Post("/travel/random-player");
        Permissions("web_access");
        Description(b => b.WithSummary("随机获取玩家信息 (由 APP 服务返回)").WithTags("Travel"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var res = await _client.PostAsync("/social/random-player", null, ct);
        if (!res.IsSuccessStatusCode)
        {
            await HttpContext.Response.SendAsync(new RandomPlayerResponse { Success = false }, (int)res.StatusCode, cancellation: ct);
            return;
        }
        var data = await res.Content.ReadFromJsonAsync<RandomPlayerResponse>(cancellationToken: ct) ?? new RandomPlayerResponse { Success = false };
        await HttpContext.Response.SendAsync(data, 200, cancellation: ct);
    }
}
