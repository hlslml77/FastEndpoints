using FastEndpoints;
using System.Security.Claims;
using Web.Services;

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
    private readonly ILogger<Endpoint> _logger;
    public Endpoint(IInventoryService svc, ILogger<Endpoint> logger) { _svc = svc; _logger = logger; }

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
            ThrowError("equipmentRecordId 无效");
            return;
        }

        var userIdStr = User?.Claims?.FirstOrDefault(c =>
            c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            ThrowError("未能从令牌解析用户ID");
            return;
        }

        try
        {
            await _svc.EquipAsync(userId, req.EquipmentRecordId, ct);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Equip failed: equipment not found. userId={UserId}, equipmentRecordId={EquipmentRecordId}", userId, req.EquipmentRecordId);
            ThrowError("装备不存在");
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Equip failed with server error. userId={UserId}, equipmentRecordId={EquipmentRecordId}", userId, req.EquipmentRecordId);
            ThrowError("服务器内部错误");
            return;
        }

        await HttpContext.Response.SendAsync(new Response { Success = true }, 200, cancellation: ct);
    }
}

