using FastEndpoints;
using Serilog;
using System.Security.Claims;
using Web.Services;

namespace CollectionApi.Obtain;

public class Response
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? CollectionId { get; set; }
}

public class Endpoint : EndpointWithoutRequest<Response>
{
    private readonly ICollectionService _svc;

    public Endpoint(ICollectionService svc) { _svc = svc; }

    public override void Configure()
    {
        Post("/collection/obtain");
        Permissions("web_access");
        Description(x => x.WithTags("Collection").WithSummary("随机获取一个藏品（含权重与限量）"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            await HttpContext.Response.SendAsync(new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" }, 400, cancellation: ct);
            return;
        }

        var (success, message, cid) = await _svc.ObtainAsync(userId, ct);
        await HttpContext.Response.SendAsync(new Response { Success = success, Message = message, CollectionId = cid }, 200, cancellation: ct);
    }
}

