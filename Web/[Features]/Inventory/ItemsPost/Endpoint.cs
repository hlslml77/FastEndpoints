using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace InventoryApi.ItemsPost;

public class Endpoint : EndpointWithoutRequest<List<InventoryApi.ItemsGet.ResponseItem>>
{
    private readonly IInventoryService _svc;
    public Endpoint(IInventoryService svc) => _svc = svc;

    public override void Configure()
    {
        Post("/inventory/items");
        Permissions("web_access");
        Description(x => x.WithTags("Inventory").WithSummary("查询玩家道具清单"));
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

        var items = await _svc.GetItemsAsync(userId, ct);
        var resp = items.Select(i => new InventoryApi.ItemsGet.ResponseItem
        {
            ItemId = i.ItemId,
            Amount = i.Amount,
            UpdatedAt = i.UpdatedAt
        }).ToList();

        await HttpContext.Response.SendAsync(resp, 200, cancellation: ct);
    }
}

