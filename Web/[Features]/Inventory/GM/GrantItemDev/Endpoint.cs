using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace InventoryApi.GM.GrantItemDev;

public class Request
{
    public int ItemId { get; set; }
    public int Amount { get; set; } = 1;
}

public class Response
{
    public bool Success { get; set; }
}

public class Endpoint : Endpoint<Request, Response>
{
    private readonly IInventoryService _svc;
    private readonly ILogger<Endpoint> _logger;
    public Endpoint(IInventoryService svc, ILogger<Endpoint> logger) { _svc = svc; _logger = logger; }

    public override void Configure()
    {
        Post("/gm/dev/grant-item");
        // for development/testing: allow with web_access token
        Permissions("web_access");
        Description(x => x.WithTags("GM").WithSummary("[DEV] 使用 web token 给自己发道具/装备（仅开发测试）"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userIdStr = User?.Claims?.FirstOrDefault(c =>
            c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            ThrowError("未能从令牌解析用户ID");
            return;
        }

        if (req.ItemId <= 0)
        {
            ThrowError("itemId 无效");
            return;
        }
        if (req.Amount <= 0)
        {
            ThrowError("amount 必须大于0");
            return;
        }

        try
        {
            await _svc.GrantItemAsync(userId, req.ItemId, req.Amount, ct);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "[DEV] Grant item failed. userId={UserId}, itemId={ItemId}, amount={Amount}", userId, req.ItemId, req.Amount);
            ThrowError(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DEV] Grant item failed with server error. userId={UserId}, itemId={ItemId}, amount={Amount}", userId, req.ItemId, req.Amount);
            ThrowError("服务器内部错误");
            return;
        }

        await HttpContext.Response.SendAsync(new Response { Success = true }, 200, cancellation: ct);
    }
}

