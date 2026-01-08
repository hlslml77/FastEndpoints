using FastEndpoints;
using System.Security.Claims;
using Web.Services;
using Serilog;

using Web.Data;
namespace InventoryApi.ItemStatusPost;

public class Request
{
    public List<int>? ItemIds { get; set; }
}

public class ResponseItemStatus
{
    public int ItemId { get; set; }
    public long Amount { get; set; }
    /// <summary>
    /// 下次自动恢复时间（仅体力等会恢复的道具需要；无则为 0001-01-01T00:00:00Z）
    /// </summary>
    public DateTime NextRefreshTime { get; set; }
}

public class Endpoint : Endpoint<Request, List<ResponseItemStatus>>
{
    private readonly IInventoryService _inv;
    private readonly IGeneralConfigService _cfg;
    private readonly AppDbContext _db;

    public Endpoint(IInventoryService inv, IGeneralConfigService cfg, AppDbContext db)
    {
        _inv = inv; _cfg = cfg; _db = db;
    }

    public override void Configure()
    {
        Post("/inventory/item-status");
        Permissions("web_access");
        Description(x => x.WithTags("Inventory").WithSummary("批量查询道具数量（含体力下次恢复时间）"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            await HttpContext.Response.SendAsync(new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" }, 400, cancellation: ct);
            return;
        }

        var ids = req.ItemIds ?? new List<int>();
        if (ids.Count == 0)
        {
            await HttpContext.Response.SendAsync(new List<ResponseItemStatus>(), 200, cancellation: ct);
            return;
        }

        try
        {
            var items = await _inv.GetItemsAsync(userId, ct);
            var map = items.ToDictionary(i => i.ItemId, i => i);

            var staminaId = _cfg.InitialStaminaItemId;
            var maxSta = _cfg.GetStaminaMax();
            var interval = _cfg.GetStaminaRecoverIntervalMinutes();

            var resp = new List<ResponseItemStatus>();
            foreach (var id in ids)
            {
                map.TryGetValue(id, out var rec);
                var amount = rec?.Amount ?? 0;
                DateTime nextTime = DateTime.MinValue;
                if (id == staminaId)
                {
                    var last = rec?.UpdatedAt ?? DateTime.UtcNow;
                    if (amount < maxSta)
                        nextTime = last.AddMinutes(interval);
                }
                resp.Add(new ResponseItemStatus { ItemId = id, Amount = amount, NextRefreshTime = nextTime });
            }

            await HttpContext.Response.SendAsync(resp, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Query item-status failed userId={UserId}", userId);
            await HttpContext.Response.SendAsync(new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" }, 500, cancellation: ct);
        }
    }
}

