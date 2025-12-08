using FastEndpoints;
using RankApi;
using Web.Services;
using System.Security.Claims;

namespace RankApi.ClaimSeason;

public class Endpoint : Endpoint<ClaimRequest, ClaimResponse>
{
    private readonly IPveRankService _rankService;
    public Endpoint(IPveRankService rankService) { _rankService = rankService; }

    public override void Configure()
    {
        Post("rank/claim-season");
        Permissions("web_access");
        Description(x => x.WithTags("Rank").WithSummary("领取赛季榜奖励").WithDescription("根据赛季结算名次发放奖励，幂等：已领取将返回失败消息"));
    }

    public override async Task HandleAsync(ClaimRequest req, CancellationToken ct)
    {
        var userIdStr = User?.Claims?.FirstOrDefault(c => c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
        if (!long.TryParse(userIdStr, out var userId))
        {
            await HttpContext.Response.SendAsync(new ClaimResponse { Success = false, Message = "未能从令牌解析用户ID" }, 400, cancellation: ct);
            return;
        }

        var (ok, msg, rewards) = await _rankService.ClaimSeasonAsync(userId, req.DeviceType, DateTime.UtcNow);
        await HttpContext.Response.SendAsync(new ClaimResponse
        {
            Success = ok,
            Message = msg,
            Rewards = rewards.Select(r => new RewardDto { ItemId = r.itemId, Amount = r.amount }).ToList()
        }, 200, cancellation: ct);
    }
}

