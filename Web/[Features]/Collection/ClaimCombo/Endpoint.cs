using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace CollectionApi.ClaimCombo;

public class Request
{
    public int ComboId { get; set; }
}

public class Response
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class Endpoint : Endpoint<Request, Response>
{
    private readonly ICollectionService _svc;
    public Endpoint(ICollectionService svc) { _svc = svc; }

    public override void Configure()
    {
        Post("/collection/claim-combo");
        Permissions("web_access");
        Description(x => x.WithTags("Collection").WithSummary("领取组合奖励"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            await HttpContext.Response.SendAsync(new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" }, 400, cancellation: ct);
            return;
        }

        var (ok, msg) = await _svc.ClaimComboAsync(userId, req.ComboId, ct);
        await HttpContext.Response.SendAsync(new Response { Success = ok, Message = msg }, 200, cancellation: ct);
    }
}

