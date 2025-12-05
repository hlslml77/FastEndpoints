using FastEndpoints;
using Serilog;
using System.Security.Claims;
using System.Text.Json;
using System.Linq;
using Web.Data;
using Web.Data.Entities;

namespace Travel.SaveStageMessage;

/// <summary>
/// 保存旅行关卡留言请求
/// </summary>
public class SaveStageMessageRequest
{
    /// <summary>
    /// 关卡LevelId
    /// </summary>
    public int LevelId { get; set; }

    /// <summary>
    /// 三个整型ID的列表
    /// </summary>
    public List<int> IdList { get; set; } = new();
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

            // 验证关卡LevelId
            if (req.LevelId <= 0)
            {
                var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "关卡ID无效" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 验证ID列表（必须是3个正整数）
            if (req.IdList is null || req.IdList.Count != 3 || req.IdList.Any(x => x <= 0))
            {
                var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "IdList必须包含3个大于0的整数" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 序列化为JSON存入MessageContent，避免改表结构
            var json = JsonSerializer.Serialize(req.IdList);

            // 创建记录
            var stageMessage = new TravelStageMessage
            {
                UserId = userId,
                StageId = req.LevelId,
                MessageContent = json,
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
            Log.Error(ex, "Save stage message failed. levelId={LevelId}", req.LevelId);
            var errorBody = new { statusCode = 500, code = ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

