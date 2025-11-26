using FastEndpoints;
using System.Security.Claims;
using Web.Services;
using Serilog;

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
    public Endpoint(IInventoryService svc) { _svc = svc; }

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
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
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
            await _svc.GrantItemAsync(userId, req.ItemId, req.Amount, ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "[DEV] Grant item failed. userId={UserId}, itemId={ItemId}, amount={Amount}", userId, req.ItemId, req.Amount);
            var msg = ex.Message ?? string.Empty;
            var code = msg.Contains("Equip config not found", StringComparison.OrdinalIgnoreCase)
                ? Web.Data.ErrorCodes.Inventory.EquipmentConfigNotFound
                : (msg.Contains("Item", StringComparison.OrdinalIgnoreCase) && msg.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    ? Web.Data.ErrorCodes.Inventory.ItemNotFound
                    : Web.Data.ErrorCodes.Common.BadRequest;
            var errorBody = new { statusCode = 400, code = code, message = ex.Message ?? "请求参数错误" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DEV] Grant item failed with server error. userId={UserId}, itemId={ItemId}, amount={Amount}", userId, req.ItemId, req.Amount);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(new Response { Success = true }, 200, cancellation: ct);
    }
}

