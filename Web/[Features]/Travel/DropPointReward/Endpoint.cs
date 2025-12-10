using FastEndpoints;
using Serilog;
using System.Security.Claims;
using Web.Data;
using Web.Services;
using Web.Data.Config;

namespace Travel.DropPointReward;

public class Request
{
    public int LevelId { get; set; }
    public int Distance { get; set; }
}

public class GrantedItem
{
    public int ItemId { get; set; }
    public int Amount { get; set; }
}

public class Response
{
    public bool Success { get; set; }
    public List<GrantedItem> Rewards { get; set; } = new();
}

public class Endpoint : Endpoint<Request, Response>
{
    private readonly ITravelDropPointConfigService _cfgSvc;
    private readonly IInventoryService _inventory;
    private readonly Random _rand = new();

    public Endpoint(ITravelDropPointConfigService cfgSvc, IInventoryService inventory)
    { _cfgSvc = cfgSvc; _inventory = inventory; }

    public override void Configure()
    {
        Post("/travel/drop-point/reward");
        Permissions("web_access");
        Description(x => x.WithTags("Travel").WithSummary("根据关卡ID与距离发放旅行掉落奖励"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        // parse user id
        var userIdStr = User?.Claims?.FirstOrDefault(c =>
            c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
        {
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        if (req.LevelId <= 0)
        {
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "LevelId 无效" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        var configs = _cfgSvc.GetByLevel(req.LevelId);
        if (configs.Count == 0)
        {
            var errorBody = new { statusCode = 404, code = ErrorCodes.Common.NotFound, message = "未找到对应关卡配置" };
            await HttpContext.Response.SendAsync(errorBody, 404, cancellation: ct);
            return;
        }

        // 选择配置：优先匹配 Distance 列表包含的；否则退回 Distance 为空的第一条
        TravelDropPointConfig? cfg = configs.FirstOrDefault(c => c.Distance != null && c.Distance.Contains(req.Distance))
                                   ?? configs.FirstOrDefault(c => c.Distance == null);
        if (cfg is null)
        {
            var errorBody = new { statusCode = 404, code = ErrorCodes.Common.NotFound, message = "未找到匹配的掉落点配置" };
            await HttpContext.Response.SendAsync(errorBody, 404, cancellation: ct);
            return;
        }

        var granted = new List<GrantedItem>();

        try
        {
            // 随机奖励
            if (cfg.DropRandom != null && cfg.DropRandom.Length > 0 && cfg.QuantitiesRandom != null && cfg.QuantitiesRandom.Length > 0)
            {
                int itemId = cfg.DropRandom[_rand.Next(0, cfg.DropRandom.Length)];

                int amount;
                if (cfg.QuantitiesRandom.Length == 1)
                {
                    amount = Math.Max(1, cfg.QuantitiesRandom[0]);
                }
                else
                {
                    var min = cfg.QuantitiesRandom[0];
                    var max = cfg.QuantitiesRandom[1];
                    if (max < min) { var t = min; min = max; max = t; }
                    amount = _rand.Next(min, max + 1);
                }

                await _inventory.GrantItemAsync(userId, itemId, amount, ct);
                granted.Add(new GrantedItem { ItemId = itemId, Amount = amount });
            }

            // 固定奖励（单条 [itemId, amount]）
            if (cfg.FixReward != null && cfg.FixReward.Length >= 2)
            {
                var itemId = cfg.FixReward[0];
                var amount = cfg.FixReward[1];
                if (itemId > 0 && amount > 0)
                {
                    await _inventory.GrantItemAsync(userId, itemId, amount, ct);
                    granted.Add(new GrantedItem { ItemId = itemId, Amount = amount });
                }
            }

            await HttpContext.Response.SendAsync(new Response { Success = true, Rewards = granted }, 200, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "Grant drop point reward failed. userId={UserId}, levelId={LevelId}, distance={Distance}", userId, req.LevelId, req.Distance);
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = ex.Message };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Grant drop point reward failed with server error. userId={UserId}, levelId={LevelId}, distance={Distance}", userId, req.LevelId, req.Distance);
            var errorBody = new { statusCode = 500, code = ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

