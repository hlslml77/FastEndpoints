using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace InventoryApi.EquipmentsGet;

public class ResponseEquipment
{
    public long Id { get; set; }
    public int EquipId { get; set; }
    public int Quality { get; set; }
    public int Part { get; set; }
    public int? Attack { get; set; }
    public int? HP { get; set; }
    public int? Defense { get; set; }
    public int? Critical { get; set; }
    public int? AttackSpeed { get; set; }
    public int? CriticalDamage { get; set; }
    public int? UpperLimb { get; set; }
    public int? LowerLimb { get; set; }
    public int? Core { get; set; }
    public int? HeartLungs { get; set; }
    public bool IsEquipped { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class Endpoint : EndpointWithoutRequest<List<ResponseEquipment>>
{
    private readonly IInventoryService _svc;
    private readonly ILogger<Endpoint> _logger;
    public Endpoint(IInventoryService svc, ILogger<Endpoint> logger) { _svc = svc; _logger = logger; }

    public override void Configure()
    {
        Get("/inventory/equipments");
        Permissions("web_access");
        Description(x => x.WithTags("Inventory").WithSummary("查询玩家装备清单"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
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
            var eqs = await _svc.GetEquipmentsAsync(userId, ct);
            var resp = eqs.Select(e => new ResponseEquipment
            {
                Id = e.Id,
                EquipId = e.EquipId,
                Quality = e.Quality,
                Part = e.Part,
                Attack = e.Attack,
                HP = e.HP,
                Defense = e.Defense,
                Critical = e.Critical,
                AttackSpeed = e.AttackSpeed,
                CriticalDamage = e.CriticalDamage,
                UpperLimb = e.UpperLimb,
                LowerLimb = e.LowerLimb,
                Core = e.Core,
                HeartLungs = e.HeartLungs,
                IsEquipped = e.IsEquipped,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            }).ToList();

            await HttpContext.Response.SendAsync(resp, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get equipments failed. userId={UserId}", userId);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

