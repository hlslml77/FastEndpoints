using FastEndpoints;
using Web.Services;
using Serilog;

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
    public Endpoint(IInventoryService svc) { _svc = svc; }

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
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "userId 无效" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }
        if (req.ItemId <= 0)
        {
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "itemId 无效" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }
        if (req.Amount <= 0)
        {
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "amount 必须大于0" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        try
        {
            await _svc.GrantItemAsync(req.UserId, req.ItemId, req.Amount, ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "Grant item failed. userId={UserId}, itemId={ItemId}, amount={Amount}", req.UserId, req.ItemId, req.Amount);
            var msg = ex.Message ?? string.Empty;
            var code = msg.Contains("Equip config not found", StringComparison.OrdinalIgnoreCase)
                ? Web.Data.ErrorCodes.Inventory.EquipmentConfigNotFound
                : (msg.Contains("Item", StringComparison.OrdinalIgnoreCase) && msg.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    ? Web.Data.ErrorCodes.Inventory.ItemNotFound
                    : Web.Data.ErrorCodes.Common.BadRequest;
            var errorBody = new { statusCode = 400, code = code, message = ex.Message };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Grant item failed with server error. userId={UserId}, itemId={ItemId}, amount={Amount}", req.UserId, req.ItemId, req.Amount);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(new Response { Success = true }, 200, cancellation: ct);
    }
}

