using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace InventoryApi.EquipmentsPost;

public class Endpoint : EndpointWithoutRequest<List<InventoryApi.EquipmentsGet.ResponseEquipment>>
{
    private readonly IInventoryService _svc;
    private readonly ILogger<Endpoint> _logger;
    public Endpoint(IInventoryService svc, ILogger<Endpoint> logger) { _svc = svc; _logger = logger; }

    public override void Configure()
    {
        Post("/inventory/equipments");
        Permissions("web_access");
        Description(x => x.WithTags("Inventory").WithSummary("查询玩家装备清单"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userIdStr = User?.Claims?.FirstOrDefault(c =>
            c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            ThrowError("未能从令牌解析用户ID");
            return;
        }

        try
        {
            var eqs = await _svc.GetEquipmentsAsync(userId, ct);
            var resp = eqs.Select(e => new InventoryApi.EquipmentsGet.ResponseEquipment
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
            _logger.LogError(ex, "Get equipments (POST) failed. userId={UserId}", userId);
            ThrowError("服务器内部错误");
        }
    }
}

