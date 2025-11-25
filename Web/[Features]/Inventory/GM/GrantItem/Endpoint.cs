using FastEndpoints;
using Web.Services;

namespace InventoryApi.GM.GrantItem;

public class Request
{
    public long UserId { get; set; }
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
        Post("/gm/grant-item");
        Policies("AdminOnly");
        Description(x => x.WithTags("GM").WithSummary("GM 添加道具/装备给指定玩家"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.UserId <= 0)
        {
            ThrowError("userId 无效");
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
            await _svc.GrantItemAsync(req.UserId, req.ItemId, req.Amount, ct);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Grant item failed. userId={UserId}, itemId={ItemId}, amount={Amount}", req.UserId, req.ItemId, req.Amount);
            ThrowError(ex.Message);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Grant item failed with server error. userId={UserId}, itemId={ItemId}, amount={Amount}", req.UserId, req.ItemId, req.Amount);
            ThrowError("服务器内部错误");
            return;
        }

        await HttpContext.Response.SendAsync(new Response { Success = true }, 200, cancellation: ct);
    }
}

