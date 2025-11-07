using Web.Services;
using FastEndpoints;

namespace RoleGrowth.GetPlayerRole;

/// <summary>
/// 获取玩家角色信息请求
/// </summary>
public class Request
{
    /// <summary>
    /// 用户ID
    /// </summary>
    public long UserId { get; set; }
}

/// <summary>
/// 获取玩家角色信息端点
/// </summary>
public class Endpoint : Endpoint<Request, PlayerRoleResponse>
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
        Get("/role-growth/player/{UserId}");
        AllowAnonymous();
        Description(x => x
            .WithTags("RoleGrowth")
            .WithSummary("获取玩家角色信息")
            .WithDescription("获取指定用户的角色成长信息，包括等级、经验、属性等"));
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var player = await _roleGrowthService.GetOrCreatePlayerAsync(req.UserId);
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
}

