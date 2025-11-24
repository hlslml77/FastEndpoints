using FastEndpoints;
using System.Security.Claims;
using Web.Services;

namespace InventoryApi.ItemsPost;

public class Endpoint : EndpointWithoutRequest<List<InventoryApi.ItemsGet.ResponseItem>>
{
    private readonly IInventoryService _svc;
    private readonly ILogger<Endpoint> _logger;
    public Endpoint(IInventoryService svc, ILogger<Endpoint> logger) { _svc = svc; _logger = logger; }

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

        try
        {
            var items = await _svc.GetItemsAsync(userId, ct);
            var resp = items.Select(i => new InventoryApi.ItemsGet.ResponseItem
            {
                ItemId = i.ItemId,
                Amount = i.Amount,
                UpdatedAt = i.UpdatedAt
            }).ToList();

            await HttpContext.Response.SendAsync(resp, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get items (POST) failed. userId={UserId}", userId);
            ThrowError("服务器内部错误");
        }
    }
}

