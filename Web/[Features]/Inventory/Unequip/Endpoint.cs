using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace InventoryApi.Unequip;

public class Request
{
    public long EquipmentRecordId { get; set; }
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
        Post("/inventory/unequip");
        Permissions("web_access");
        Description(x => x.WithTags("Inventory").WithSummary("卸下指定装备"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        if (req.EquipmentRecordId <= 0)
        {
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "equipmentRecordId 无效" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        var userIdStr = User?.Claims?.FirstOrDefault(c =>
            c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        try
        {
            await _svc.UnequipAsync(userId, req.EquipmentRecordId, ct);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Unequip failed: equipment not found. userId={UserId}, equipmentRecordId={EquipmentRecordId}", userId, req.EquipmentRecordId);
            var errorBody = new { statusCode = 404, code = Web.Data.ErrorCodes.Inventory.EquipmentNotFound, message = "装备不存在" };
            await HttpContext.Response.SendAsync(errorBody, 404, cancellation: ct);
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unequip failed with server error. userId={UserId}, equipmentRecordId={EquipmentRecordId}", userId, req.EquipmentRecordId);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(new Response { Success = true }, 200, cancellation: ct);
    }
}

