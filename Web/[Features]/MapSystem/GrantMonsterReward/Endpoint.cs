using FastEndpoints;
using Serilog;
using System.Security.Claims;
using Web.Data;
using Web.Services;


namespace MapSystem.GrantMonsterReward;

public class Request
{
    /// <summary>
    /// 单个怪物ID（兼容旧客户端；若同时传 MonsterIds 则两者会合并去重）
    /// </summary>
    public int? MonsterId { get; set; }

    /// <summary>
    /// 多个怪物ID列表
    /// </summary>
    public List<int>? MonsterIds { get; set; }
}

public class Response
{
    /// <summary>
    /// 是否至少成功发放了一条奖励
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 合并后的奖励列表
    /// </summary>
    public List<MapSystem.RewardItem> Rewards { get; set; } = new();

    /// <summary>
    /// 请求中未找到配置的 MonsterId 列表
    /// </summary>
    public List<int> FailedMonsterIds { get; set; } = new();
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

        // 收集怪物 ID
        var monsterIds = new List<int>();
        if (req.MonsterIds != null && req.MonsterIds.Count > 0)
            monsterIds.AddRange(req.MonsterIds.Where(id => id > 0));
        if (req.MonsterId.HasValue && req.MonsterId.Value > 0)
            monsterIds.Add(req.MonsterId.Value);

        if (monsterIds.Count == 0)
        {
            var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "MonsterId 无效" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
            return;
        }

        try
        {
            // 先把多个怪物的奖励汇总（同 itemId 合并数量），再一次性下发
            var rewardDict = new Dictionary<int, int>();
            var failedMonsterIds = new List<int>();
            var hasValidRewards = false;

            foreach (var monsterId in monsterIds.Distinct())
            {
                var cfg = _monsterCfg.GetById(monsterId);
                if (cfg is null)
                {
                    failedMonsterIds.Add(monsterId);
                    continue;
                }

                if (cfg.Reward == null || cfg.Reward.Count == 0)
                {
                    // 没有配置奖励的也计入失败
                    failedMonsterIds.Add(monsterId);
                    continue;
                }

                var hasValidReward = false;
                foreach (var pair in cfg.Reward)
                {
                    if (pair == null || pair.Count < 2) continue;
                    var itemId = pair[0];
                    var amount = pair[1];
                    if (itemId <= 0 || amount <= 0) continue;

                    rewardDict[itemId] = rewardDict.TryGetValue(itemId, out var old) ? old + amount : amount;
                    hasValidReward = true;
                }

                if (!hasValidReward)
                {
                    // 有配置但奖励项都无效的也计入失败
                    failedMonsterIds.Add(monsterId);
                }
                else
                {
                    hasValidRewards = true;
                }
            }

            if (!hasValidRewards && failedMonsterIds.Count > 0)
            {
                // 所有请求的怪物ID都无效
                var errorBody = new { statusCode = 404, code = ErrorCodes.Common.NotFound, message = "未找到有效的怪物配置" };
                await HttpContext.Response.SendAsync(errorBody, 404, cancellation: ct);
                return;
            }

            var rewards = new List<MapSystem.RewardItem>();
            foreach (var (itemId, amount) in rewardDict)
            {
                await _inventory.GrantItemAsync(userId, itemId, amount, ct);
                rewards.Add(new MapSystem.RewardItem { ItemId = itemId, Amount = amount });
            }

            await HttpContext.Response.SendAsync(new Response
            {
                Success = rewards.Count > 0,  // 只要成功发放了奖励就算成功
                Rewards = rewards,
                FailedMonsterIds = failedMonsterIds
            }, 200, cancellation: ct);
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

