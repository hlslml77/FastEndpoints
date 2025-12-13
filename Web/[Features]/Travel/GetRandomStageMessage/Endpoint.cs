using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Security.Claims;
using System.Text.Json;
using System.Linq;
using Web.Data;

namespace Travel.GetRandomStageMessage;

/// <summary>
/// 获取随机关卡留言请求
/// </summary>
public class GetRandomStageMessageRequest
{
    /// <summary>
    /// 关卡LevelId
    /// </summary>
    public int LevelId { get; set; }
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
    /// 留言用户ID
    /// </summary>
    public long UserId { get; set; }

    /// <summary>
    /// 三个整型ID的列表（保存时的ID列表）
    /// </summary>
    public List<int> MessageIDList { get; set; } = new();

    /// <summary>
    /// 创建时间
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
            if (req.LevelId <= 0)
            {
                var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "关卡LevelId无效" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 获取该关卡的所有留言
            var messages = await _dbContext.TravelStageMessage
                .Where(m => m.StageId == req.LevelId)
                .ToListAsync(ct);

            if (messages.Count == 0)
            {
                // 没有留言时返回空对象（列表为空）
                await HttpContext.Response.SendAsync(new GetRandomStageMessageResponse
                {
                    Success = true,
                    UserId = 0,
                    MessageIDList = new List<int>(),
                    CreatedAt = DateTime.MinValue
                }, 200, cancellation: ct);
                return;
            }

            // 查询当前用户在该关卡自己保存过的所有ID集合
            var ownContents = await _dbContext.TravelStageMessage
                .Where(m => m.StageId == req.LevelId && m.UserId == userId)
                .Select(m => m.MessageContent)
                .ToListAsync(ct);

            var ownSavedIds = new HashSet<int>();
            foreach (var content in ownContents)
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<int>>(content);
                    if (list != null)
                    {
                        foreach (var id in list)
                            ownSavedIds.Add(id);
                    }
                }
                catch
                {
                    // ignore malformed json
                }
            }

            // 随机选择一条留言
            var randomMessage = messages[_rand.Next(0, messages.Count)];

            // 从 MessageContent 反序列化出 IdList
            List<int>? idList = null;
            try
            {
                idList = JsonSerializer.Deserialize<List<int>>(randomMessage.MessageContent) ?? new List<int>();
            }
            catch
            {
                idList = new List<int>();
            }

            // 需求：去掉自己保存过的消息ID
            if (ownSavedIds.Count > 0 && idList is not null)
            {
                idList = idList.Where(id => !ownSavedIds.Contains(id)).ToList();
            }

            await HttpContext.Response.SendAsync(new GetRandomStageMessageResponse
            {
                Success = true,
                UserId = randomMessage.UserId,
                MessageIDList = idList ?? new List<int>(),
                CreatedAt = randomMessage.CreatedAt
            }, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Get random stage message failed. levelId={LevelId}", req.LevelId);
            var errorBody = new { statusCode = 500, code = ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}
