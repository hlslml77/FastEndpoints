using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace CollectionApi.My;

public class Response
{
    public List<int> CollectionIds { get; set; } = new();
}

public class Endpoint : EndpointWithoutRequest<Response>
{
    private readonly ICollectionService _svc;
    public Endpoint(ICollectionService svc) { _svc = svc; }

    public override void Configure()
    {
        Get("/collection/my");
        Permissions("web_access");
        Description(x => x.WithTags("Collection").WithSummary("获取玩家已拥有的藏品ID列表"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            await HttpContext.Response.SendAsync(new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" }, 400, cancellation: ct);
            return;
        }

        var ids = await _svc.GetMyCollectionIdsAsync(userId, ct);
        await HttpContext.Response.SendAsync(new Response { CollectionIds = ids }, 200, cancellation: ct);
    }
}

