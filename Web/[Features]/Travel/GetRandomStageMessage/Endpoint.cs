using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Security.Claims;
using Web.Data;

namespace Travel.GetRandomStageMessage;

/// <summary>
/// 获取随机关卡留言请求
/// </summary>
public class GetRandomStageMessageRequest
{
    /// <summary>
    /// 关卡ID
    /// </summary>
    public int StageId { get; set; }
}

/// <summary>
/// 获取随机关卡留言响应
/// </summary>
public class GetRandomStageMessageResponse
{
    /// <summary>
    /// 是否获取成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 留言ID
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 留言用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 留言内容
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 留言创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 获取随机关卡留言端点
/// </summary>
public class Endpoint : Endpoint<GetRandomStageMessageRequest, GetRandomStageMessageResponse>
{
    private readonly AppDbContext _dbContext;
    private readonly Random _rand = new();

    public Endpoint(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post("/travel/stage/get-random-message");
        Permissions("web_access");
        Description(x => x
            .WithTags("Travel")
            .WithSummary("获取随机关卡留言")
            .WithDescription("玩家在跑关卡的过程中可以获取本关所有玩家的留言中的随机一条"));
    }

    public override async Task HandleAsync(GetRandomStageMessageRequest req, CancellationToken ct)
    {
        try
        {
            // 解析用户ID
            var userIdStr = User?.Claims?.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
            {
                var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 验证关卡ID
            if (req.StageId <= 0)
            {
                var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "关卡ID无效" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 获取该关卡的所有留言
            var messages = await _dbContext.TravelStageMessage
                .Where(m => m.StageId == req.StageId)
                .ToListAsync(ct);

            if (messages.Count == 0)
            {
                // 没有留言时返回空结果
                await HttpContext.Response.SendAsync(new GetRandomStageMessageResponse
                {
                    Success = true,
                    MessageId = 0,
                    UserId = 0,
                    Message = string.Empty,
                    CreatedAt = DateTime.MinValue
                }, 200, cancellation: ct);
                return;
            }

            // 随机选择一条留言
            var randomMessage = messages[_rand.Next(0, messages.Count)];

            await HttpContext.Response.SendAsync(new GetRandomStageMessageResponse
            {
                Success = true,
                MessageId = randomMessage.Id,
                UserId = randomMessage.UserId,
                Message = randomMessage.MessageContent,
                CreatedAt = randomMessage.CreatedAt
            }, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Get random stage message failed. stageId={StageId}", req.StageId);
            var errorBody = new { statusCode = 500, code = ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

