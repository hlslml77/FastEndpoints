using FastEndpoints;
using Serilog;
using System.Security.Claims;
using Web.Data;
using Web.Services;

namespace MapSystem.GrantMonsterReward;

public class Request
{
    public int MonsterId { get; set; }
}

public class Response
{
    public bool Success { get; set; }
    public List<MapSystem.RewardItem> Rewards { get; set; } = new();
}

public class Endpoint : Endpoint<Request, Response>
{
    private readonly IMonsterConfigService _monsterCfg;
    private readonly IInventoryService _inventory;

    public Endpoint(IMonsterConfigService monsterCfg, IInventoryService inventory)
    {
        _monsterCfg = monsterCfg;
        _inventory = inventory;
    }

    public override void Configure()
    {
        Post("/map/monster/reward");
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem", "exclude")
            .WithSummary("根据MonsterID发放奖励(大地图)")
            .WithDescription("客户端传入 Monster.json 的 ID，服务端发放该配置的 Reward 列表。此接口不在自动文档中展示。"));
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

        if (req.MonsterId <= 0)
        {
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "MonsterId 无效" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        try
        {
            var cfg = _monsterCfg.GetById(req.MonsterId);
            if (cfg is null)
            {
                var errorBody = new { statusCode = 404, code = ErrorCodes.Common.NotFound, message = "未找到对应怪物配置" };
                await HttpContext.Response.SendAsync(errorBody, 404, cancellation: ct);
                return;
            }

            var rewards = new List<MapSystem.RewardItem>();
            if (cfg.Reward != null && cfg.Reward.Count > 0)
            {
                foreach (var pair in cfg.Reward)
                {
                    if (pair == null || pair.Count < 2) continue;
                    var itemId = pair[0];
                    var amount = pair[1];
                    if (itemId <= 0 || amount <= 0) continue;

                    await _inventory.GrantItemAsync(userId, itemId, amount, ct);
                    rewards.Add(new MapSystem.RewardItem { ItemId = itemId, Amount = amount });
                }
            }

            await HttpContext.Response.SendAsync(new Response { Success = true, Rewards = rewards }, 200, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "Grant monster reward failed. userId={UserId}, monsterId={MonsterId}", userId, req.MonsterId);
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = ex.Message };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Grant monster reward failed with server error. userId={UserId}, monsterId={MonsterId}", userId, req.MonsterId);
            var errorBody = new { statusCode = 500, code = ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

