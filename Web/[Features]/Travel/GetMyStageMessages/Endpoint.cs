using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Security.Claims;
using System.Text.Json;
using System.Linq;
using Web.Data;

namespace Travel.GetMyStageMessages;

public class GetMyStageMessagesRequest
{
    /// <summary>
    /// 关卡LevelId
    /// </summary>
    public int LevelId { get; set; }
}

public class MyStageMessageItem
{
    /// <summary>
    /// 节点ID
    /// </summary>
    public int NodeId { get; set; }

    /// <summary>
    /// 保存的三个整型ID列表
    /// </summary>
    public List<int> MessageIDList { get; set; } = new();

    /// <summary>
    /// 留言记录ID（自增主键）
    /// </summary>
    public long MessageId { get; set; }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

public class GetMyStageMessagesResponse
{
    public bool Success { get; set; }
    public int LevelId { get; set; }
    public List<MyStageMessageItem> Items { get; set; } = new();
}

/// <summary>
/// 获取“当前玩家在某关卡的所有节点留言”（每节点返回一条：按创建时间取最新）
/// </summary>
public class Endpoint : Endpoint<GetMyStageMessagesRequest, GetMyStageMessagesResponse>
{
    private readonly AppDbContext _dbContext;

    public Endpoint(AppDbContext dbContext)
    { _dbContext = dbContext; }

    public override void Configure()
    {
        Post("/travel/stage/my-messages");
        Permissions("web_access");
        Description(x => x
            .WithTags("Travel")
            .WithSummary("获取我在某关卡的所有节点留言（每节点最新一条）")
            .WithDescription("客户端传入关卡ID，服务端返回该玩家在该关卡下各节点最近一次保存的留言（三个整型ID列表）"));
    }

    public override async Task HandleAsync(GetMyStageMessagesRequest req, CancellationToken ct)
    {
        try
        {
            // user id
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
                var errorBody = new { statusCode = 400, code = ErrorCodes.Common.BadRequest, message = "关卡LevelId无效" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 读取该玩家在该关卡的全部留言，按节点分组，取每组最新一条
            var records = await _dbContext.TravelStageMessage
                .Where(m => m.StageId == req.LevelId && m.UserId == userId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(ct);

            var latestPerNode = records
                .GroupBy(r => r.NodeId)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).First())
                .ToList();

            var items = new List<MyStageMessageItem>(latestPerNode.Count);
            foreach (var r in latestPerNode)
            {
                List<int> ids;
                try { ids = JsonSerializer.Deserialize<List<int>>(r.MessageContent) ?? new(); }
                catch { ids = new(); }

                items.Add(new MyStageMessageItem
                {
                    NodeId = r.NodeId,
                    MessageIDList = ids,
                    MessageId = r.Id,
                    CreatedAt = r.CreatedAt
                });
            }

            await HttpContext.Response.SendAsync(new GetMyStageMessagesResponse
            {
                Success = true,
                LevelId = req.LevelId,
                Items = items
            }, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Get my stage messages failed. levelId={LevelId}", req.LevelId);
            var errorBody = new { statusCode = 500, code = ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

