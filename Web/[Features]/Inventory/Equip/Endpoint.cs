using FastEndpoints;
using System.Security.Claims;
using Web.Services;
using Serilog;

namespace InventoryApi.Equip;

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
    public Endpoint(IInventoryService svc) { _svc = svc; }

    public override void Configure()
    {
        Post("/inventory/equip");
        Permissions("web_access");
        Description(x => x.WithTags("Inventory").WithSummary("穿戴指定装备"));
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
            await _svc.EquipAsync(userId, req.EquipmentRecordId, ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "Equip failed: equipment not found. userId={UserId}, equipmentRecordId={EquipmentRecordId}", userId, req.EquipmentRecordId);
            var errorBody = new { statusCode = 404, code = Web.Data.ErrorCodes.Inventory.EquipmentNotFound, message = "装备不存在" };
            await HttpContext.Response.SendAsync(errorBody, 404, cancellation: ct);
            return;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Equip failed with server error. userId={UserId}, equipmentRecordId={EquipmentRecordId}", userId, req.EquipmentRecordId);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(new Response { Success = true }, 200, cancellation: ct);
    }
}

