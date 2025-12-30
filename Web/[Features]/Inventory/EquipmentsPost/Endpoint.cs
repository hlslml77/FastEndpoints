using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace InventoryApi.EquipmentsPost;

using Web.Data;

public class Endpoint : EndpointWithoutRequest<InventoryApi.EquipmentsGet.EquipmentsResponse>
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
            var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        try
        {
            var eqs = await _svc.GetEquipmentsAsync(userId, ct);
            var resp = new InventoryApi.EquipmentsGet.EquipmentsResponse
            {
                Equipments = eqs.Select(e =>
                {
                    var dto = new InventoryApi.EquipmentsGet.ResponseEquipment
                    {
                        Id = e.Id,
                        EquipId = e.EquipId,
                        Quality = e.Quality,
                        Part = e.Part,
                        SpecialEntryId = e.SpecialEntryId,
                        IsEquipped = e.IsEquipped,
                        CreatedAt = e.CreatedAt,
                        UpdatedAt = e.UpdatedAt
                    };

                    void AddInt(int? val, int type)
                    {
                        if (val.HasValue) dto.Attrs.Add(new InventoryApi.EquipmentsGet.AttrDto { Type = type, Value = val.Value });
                    }
                    void AddDouble(double? val, int type)
                    {
                        if (val.HasValue) dto.Attrs.Add(new InventoryApi.EquipmentsGet.AttrDto { Type = type, Value = val.Value });
                    }

                    AddInt(e.Attack, (int)EquipBuffType.Attack);
                    AddInt(e.HP, (int)EquipBuffType.Hp);
                    AddInt(e.Defense, (int)EquipBuffType.Defense);
                    AddInt(e.Critical, (int)EquipBuffType.Critical);
                    AddInt(e.AttackSpeed, (int)EquipBuffType.AttackSpeed);
                    AddInt(e.CriticalDamage, (int)EquipBuffType.CriticalDamage);
                    AddInt(e.UpperLimb, (int)PlayerBuffType.UpperLimb);
                    AddInt(e.LowerLimb, (int)PlayerBuffType.LowerLimb);
                    AddInt(e.Core, (int)PlayerBuffType.CoreRange);
                    AddInt(e.HeartLungs, (int)PlayerBuffType.HeartLungs);
                    AddDouble(e.Efficiency, (int)EquipBuffType.Efficiency);
                    AddDouble(e.Energy, (int)EquipBuffType.Energy);
                    AddDouble(e.Speed, (int)EquipBuffType.Speed);

                    return dto;
                }).ToList()
            };

            await HttpContext.Response.SendAsync(resp, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get equipments (POST) failed. userId={UserId}", userId);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

