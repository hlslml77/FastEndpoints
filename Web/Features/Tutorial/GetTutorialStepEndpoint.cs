using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Web.Data;

namespace Web.Features.Tutorial;

/// <summary>
/// 获取当前玩家的新手引导步骤
/// </summary>
public class GetTutorialStepRequest
{
}

public class GetTutorialStepResponse
{
    public int StepId { get; set; }
}

public class GetTutorialStepEndpoint : EndpointWithoutRequest<GetTutorialStepResponse>
{
    private readonly AppDbContext _db;

    public GetTutorialStepEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/api/tutorial/get-step");
        Tags("Tutorial");
        Description(b => b.WithName("Get Tutorial Step").WithDescription("获取玩家当前的新手引导步骤ID"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!long.TryParse(User?.Identity?.Name, out var userId))
        {
            await HttpContext.Response.SendAsync(new GetTutorialStepResponse { StepId = 0 }, 401, cancellation: ct);
            return;
        }

        var record = await _db.PlayerTutorialProgress.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId, ct);
        var stepId = record?.CurrentStepId ?? 0;
        await HttpContext.Response.SendAsync(new GetTutorialStepResponse { StepId = stepId }, cancellation: ct);
    }
}
