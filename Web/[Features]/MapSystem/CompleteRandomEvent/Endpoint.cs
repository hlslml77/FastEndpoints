using FastEndpoints;
using Serilog;
using System.Security.Claims;
using Web.Services;

namespace MapSystem.CompleteRandomEvent;

/// <summary>
/// 完成每日随机事件
/// </summary>
public class Endpoint : Endpoint<CompleteRandomEventRequest, CompleteRandomEventResponse>
{
    private readonly IMapService _mapService;

    public Endpoint(IMapService mapService)
    {
        _mapService = mapService;
    }

    public override void Configure()
    {
        Post("/map/complete-random-event");
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("完成每日随机事件")
            .WithDescription("根据 WorldUiMap_RandomEvent 的 Consumption/FixedReward 进行消耗与奖励发放"));
    }

    public override async Task HandleAsync(CompleteRandomEventRequest req, CancellationToken ct)
    {
        try
        {
            var userIdStr = User?.Claims?.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
            {
                var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            var (success, rewards) = await _mapService.CompleteRandomEventAsync(userId, req.LocationId, req.EventId);
            if (!success)
            {
                var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.Unprocessable, message = "无法完成事件（可能是未生成、事件不匹配或物品不足）" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            List<RewardItem>? rewardItems = null;
            if (rewards != null && rewards.Count > 0)
            {
                rewardItems = rewards.Select(r => new RewardItem { ItemId = r[0], Amount = r[1] }).ToList();
            }

            await HttpContext.Response.SendAsync(new CompleteRandomEventResponse
            {
                Success = true,
                Rewards = rewardItems
            }, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CompleteRandomEvent failed");
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

