using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Web.Data;

namespace Web.Features.Tutorial;

/// <summary>
/// 设置当前玩家的新手引导步骤
/// </summary>
public class SetTutorialStepRequest
{
    /// <summary>
    /// 步骤ID
    /// </summary>
    public int StepId { get; set; }
}

/// <summary>
/// 通用成功响应
/// </summary>
public class SuccessResponse
{
    public bool Success { get; set; } = true;
    public int StepId { get; set; }
}

public class SetTutorialStepEndpoint : Endpoint<SetTutorialStepRequest, SuccessResponse>
{
    private readonly AppDbContext _db;

    public SetTutorialStepEndpoint(AppDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Post("/api/tutorial/set-step");
        Tags("Tutorial");
        Description(b => b.WithName("Set Tutorial Step").WithDescription("保存玩家的新手引导步骤ID"));
    }

    public override async Task HandleAsync(SetTutorialStepRequest req, CancellationToken ct)
    {
        if (req.StepId <= 0)
        {
            await HttpContext.Response.SendAsync(new SuccessResponse { Success = false, StepId = 0 }, 400, cancellation: ct);
            return;
        }

        if (!long.TryParse(User?.Identity?.Name, out var userId))
        {
            await HttpContext.Response.SendAsync(new SuccessResponse { Success = false, StepId = 0 }, 401, cancellation: ct);
            return;
        }

        var record = await _db.PlayerTutorialProgress.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (record is null)
        {
            record = new Web.Data.Entities.PlayerTutorialProgress
            {
                UserId = userId,
                CurrentStepId = req.StepId,
                LastUpdated = DateTime.UtcNow
            };
            await _db.PlayerTutorialProgress.AddAsync(record, ct);
        }
        else
        {
            record.CurrentStepId = req.StepId;
            record.LastUpdated = DateTime.UtcNow;
            _db.PlayerTutorialProgress.Update(record);
        }

        await _db.SaveChangesAsync(ct);

        await HttpContext.Response.SendAsync(new SuccessResponse { Success = true, StepId = req.StepId }, cancellation: ct);
    }
}
