using FastEndpoints;
using Serilog;
using System.Security.Claims;
using Web.Data;
using Web.Data.Entities;

namespace Travel.SaveStageMessage;

/// <summary>
/// 保存旅行关卡留言请求
/// </summary>
public class SaveStageMessageRequest
{
    /// <summary>
    /// 关卡ID
    /// </summary>
    public int StageId { get; set; }

    /// <summary>
    /// 留言内容
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 保存旅行关卡留言响应
/// </summary>
public class SaveStageMessageResponse
{
    /// <summary>
    /// 是否保存成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 留言ID
    /// </summary>
    public long MessageId { get; set; }
}

/// <summary>
/// 保存旅行关卡留言端点
/// </summary>
public class Endpoint : Endpoint<SaveStageMessageRequest, SaveStageMessageResponse>
{
    private readonly AppDbContext _dbContext;

    public Endpoint(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public override void Configure()
    {
        Post("/travel/stage/save-message");
        Permissions("web_access");
        Description(x => x
            .WithTags("Travel")
            .WithSummary("保存旅行关卡留言")
            .WithDescription("玩家在完成关卡后可以输入一句话进行留言，该留言会被保存到数据库"));
    }

    public override async Task HandleAsync(SaveStageMessageRequest req, CancellationToken ct)
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

            // 验证留言内容
            if (string.IsNullOrWhiteSpace(req.Message))
            {
                var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "留言内容不能为空" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 限制留言长度
            var message = req.Message.Trim();
            if (message.Length > 500)
            {
                var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "留言内容过长，最多500个字符" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 创建留言记录
            var stageMessage = new TravelStageMessage
            {
                UserId = userId,
                StageId = req.StageId,
                MessageContent = message,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.TravelStageMessage.Add(stageMessage);
            await _dbContext.SaveChangesAsync(ct);

            await HttpContext.Response.SendAsync(new SaveStageMessageResponse
            {
                Success = true,
                MessageId = stageMessage.Id
            }, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Save stage message failed. stageId={StageId}", req.StageId);
            var errorBody = new { statusCode = 500, code = ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

