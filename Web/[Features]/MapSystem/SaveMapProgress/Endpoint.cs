using Web.Services;
using FastEndpoints;

namespace MapSystem.SaveMapProgress;

/// <summary>
/// 保存地图进度端点
/// </summary>
public class Endpoint : Endpoint<SaveMapProgressRequest, SaveMapProgressResponse>
{
    private readonly IMapService _mapService;

    public Endpoint(IMapService mapService)
    {
        _mapService = mapService;
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
            // 验证输入
            if (req.DistanceMeters <= 0)
            {
                ThrowError("距离必须大于0");
                return;
            }

            var progress = await _mapService.SaveMapProgressAsync(
                req.UserId,
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

            await Send.OkAsync(response, ct);
        }
        catch (Exception ex)
        {
            ThrowError(ex.Message);
        }
    }
}

