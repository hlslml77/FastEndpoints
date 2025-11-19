using Web.Services;
using FastEndpoints;

namespace RoleGrowth.CompleteSport;

/// <summary>
/// 完成运动端点
/// </summary>
public class Endpoint : Endpoint<CompleteSportRequest, PlayerRoleResponse>
{
    private readonly IPlayerRoleGrowthService _roleGrowthService;
    private readonly IRoleConfigService _configService;

    public Endpoint(IPlayerRoleGrowthService roleGrowthService, IRoleConfigService configService)
    {
        _roleGrowthService = roleGrowthService;
        _configService = configService;
    }

    public override void Configure()
    {
        Post("/role-growth/complete-sport");
        // 需要JWT token验证，要求web_access权限
        Permissions("web_access");
        Description(x => x
            .WithTags("RoleGrowth")
            .WithSummary("完成运动")
            .WithDescription("记录玩家完成的运动，根据运动类型和距离增加对应属性。运动类型: Bicycle, Run, Rowing。需要JWT token验证。"));
    }

    public override async Task HandleAsync(CompleteSportRequest req, CancellationToken ct)
    {
        try
        {
            var player = await _roleGrowthService.CompleteSportAsync(req.UserId, req.DeviceType, req.Distance, req.Calorie);
            var config = _configService.GetRoleConfig();
            var speedBonus = config.CalculateSpeedBonus(
                player.AttrUpperLimb,
                player.AttrLowerLimb,
                player.AttrCore,
                player.AttrHeartLungs);
            var nextLevelExp = _configService.GetExperienceForLevel(player.CurrentLevel);

            var response = new PlayerRoleResponse
            {
                UserId = player.UserId,
                CurrentLevel = player.CurrentLevel,
                CurrentExperience = player.CurrentExperience,
                ExperienceToNextLevel = nextLevelExp,
                UpperLimb = player.AttrUpperLimb,
                LowerLimb = player.AttrLowerLimb,
                Core = player.AttrCore,
                HeartLungs = player.AttrHeartLungs,
                TodayAttributePoints = player.TodayAttributePoints,
                AvailableAttributePoints = config.DailyAttributePointsLimit - player.TodayAttributePoints,
                SpeedBonus = speedBonus,
                LastUpdateTime = player.LastUpdateTime
            };

            await Send.OkAsync(response, ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

