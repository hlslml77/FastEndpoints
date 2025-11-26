using FastEndpoints;
using Serilog;
using System.Security.Claims;
using Web.Data;
using Web.Services;

namespace Travel.GrantRewardFromEvent;

public class Request
{
    public int EventId { get; set; }
}

public class Response
{
    public bool Success { get; set; }
    public int ItemId { get; set; }
    public int Amount { get; set; }
}

public class Endpoint : Endpoint<Request, Response>
{
    private readonly ITravelEventConfigService _travelCfg;
    private readonly IInventoryService _inventory;
    private readonly Random _rand = new();

    public Endpoint(ITravelEventConfigService travelCfg, IInventoryService inventory)
    { _travelCfg = travelCfg; _inventory = inventory; }

    public override void Configure()
    {
        Post("/travel/event/reward");
        Permissions("web_access");
        Description(x => x.WithTags("Travel").WithSummary("根据事件ID随机发放奖励"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // get user id from token
        var userIdStr = User?.Claims?.FirstOrDefault(c =>
            c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        if (req.EventId <= 0)
        {
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "EventId 无效" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        var cfg = _travelCfg.GetById(req.EventId);
        if (cfg is null)
        {
            var errorBody = new { statusCode = 404, code = ErrorCodes.Common.NotFound, message = "事件不存在" };
            await HttpContext.Response.SendAsync(errorBody, 404, cancellation: ct);
            return;
        }

        try
        {
            // pick item
            if (cfg.ResourceRandom == null || cfg.ResourceRandom.Length == 0)
                throw new ArgumentException("ResourceRandom 配置为空");
            int itemId = cfg.ResourceRandom[_rand.Next(0, cfg.ResourceRandom.Length)];

            // pick amount
            int amount;
            if (cfg.DropRandom == null || cfg.DropRandom.Length == 0)
            {
                throw new ArgumentException("DropRandom 配置为空");
            }
            else if (cfg.DropRandom.Length == 1)
            {
                amount = Math.Max(1, cfg.DropRandom[0]);
            }
            else
            {
                var min = cfg.DropRandom[0];
                var max = cfg.DropRandom[1];
                if (max < min) { var t = min; min = max; max = t; }
                amount = _rand.Next(min, max + 1);
            }

            await _inventory.GrantItemAsync(userId, itemId, amount, ct);

            await HttpContext.Response.SendAsync(new Response { Success = true, ItemId = itemId, Amount = amount }, 200, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "Grant reward from travel event failed. userId={UserId}, eventId={EventId}", userId, req.EventId);
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = ex.Message };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Grant reward from travel event failed with server error. userId={UserId}, eventId={EventId}", userId, req.EventId);
            var errorBody = new { statusCode = 500, code = ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

