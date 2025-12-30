using FastEndpoints;
using System.Security.Claims;
using Web.Services;
using Web.Data;
using Serilog;

namespace InventoryApi.EquipmentsGet;

public class AttrDto
{
    public int Type { get; set; }
    public double Value { get; set; }
}

public class ResponseEquipment
{
    public long Id { get; set; }
    public int EquipId { get; set; }
    public int Quality { get; set; }
    public int Part { get; set; }
    public List<AttrDto> Attrs { get; set; } = new();
    public int? SpecialEntryId { get; set; }
    public bool IsEquipped { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EquipmentsResponse
{
    public List<ResponseEquipment> Equipments { get; set; } = new();
}

public class Endpoint : EndpointWithoutRequest<EquipmentsResponse>
{
    private readonly IInventoryService _svc;
    public Endpoint(IInventoryService svc) { _svc = svc; }

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
            var resp = new EquipmentsResponse
            {
                Equipments = eqs.Select(e =>
                {
                    var dto = new ResponseEquipment
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
                        if (val.HasValue) dto.Attrs.Add(new AttrDto { Type = type, Value = val.Value });
                    }
                    void AddDouble(double? val, int type)
                    {
                        if (val.HasValue) dto.Attrs.Add(new AttrDto { Type = type, Value = val.Value });
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
            Log.Error(ex, "Get equipments failed. userId={UserId}", userId);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

