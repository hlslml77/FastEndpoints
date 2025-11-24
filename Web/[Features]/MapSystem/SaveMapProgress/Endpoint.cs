using Web.Services;
using FastEndpoints;
using System.Security.Claims;

namespace MapSystem.SaveMapProgress;

/// <summary>
/// 保存地图进度端点
/// </summary>
public class Endpoint : Endpoint<SaveMapProgressRequest, SaveMapProgressResponse>
{
    private readonly IMapService _mapService;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(IMapService mapService, ILogger<Endpoint> logger)
    {
        _mapService = mapService;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/map/save-progress");
        // 需要JWT token验证，要求web_access权限
        Permissions("web_access");
        Description(x => x
            .WithTags("MapSystem")
            .WithSummary("保存地图进度")
            .WithDescription("保存玩家从起点到终点的跑步进度，记录跑步距离。需要JWT token验证。"));
    }

    public override async Task HandleAsync(SaveMapProgressRequest req, CancellationToken ct)
    {
        try
        {
            // 从JWT解析用户ID（优先 sub，其次 userId/NameIdentifier）
            var userIdStr = User?.Claims?.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
            {
                ThrowError("未能从令牌解析用户ID");
                return;
            }

            // 验证输入
            if (req.DistanceMeters <= 0)
            {
                ThrowError("距离必须大于0");
                return;
            }

            var progress = await _mapService.SaveMapProgressAsync(
                userId,
                req.StartLocationId,
                req.EndLocationId,
                req.DistanceMeters);

            var response = new SaveMapProgressResponse
            {
                Id = progress.Id,
                UserId = progress.UserId,
                StartLocationId = progress.StartLocationId,
                EndLocationId = progress.EndLocationId,
                DistanceMeters = progress.DistanceMeters,
                CreatedAt = progress.CreatedAt
            };

            await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "SaveMapProgress argument error. start={Start}, end={End}, dist={Dist}", req.StartLocationId, req.EndLocationId, req.DistanceMeters);
            ThrowError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveMapProgress failed");
            ThrowError("服务器内部错误");
        }
    }
}

