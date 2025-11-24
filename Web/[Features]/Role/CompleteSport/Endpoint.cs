using Web.Services;
using FastEndpoints;
using System.Security.Claims;
using RoleApi;

namespace RoleApi.CompleteSport;

/// <summary>
/// 完成运动端点
/// </summary>
public class Endpoint : Endpoint<CompleteSportRequest, PlayerRoleResponse>
{
    private readonly IPlayerRoleService _roleGrowthService;
    private readonly IRoleConfigService _configService;

    public Endpoint(IPlayerRoleService roleGrowthService, IRoleConfigService configService)
    {
        _roleGrowthService = roleGrowthService;
        _configService = configService;
    }

    public override void Configure()
    {
        Post("/role/complete-sport");
        // 需要JWT token验证，要求web_access权限
        Permissions("web_access");
        Description(x => x
            .WithTags("Role")
            .WithSummary("完成运动")
            .WithDescription("记录玩家完成的运动，根据运动类型和距离增加对应属性。运动类型: Bicycle, Run, Rowing。需要JWT token验证。"));
    }

    public override async Task HandleAsync(CompleteSportRequest req, CancellationToken ct)
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

            var player = await _roleGrowthService.CompleteSportAsync(userId, req.DeviceType, req.Distance, req.Calorie);
            var config = _configService.GetRoleConfig();
            var nextLevelExp = _configService.GetExperienceForLevel(player.CurrentLevel);

            // 即时计算副属性（不落库）
            var sec = _roleGrowthService.ComputeSecondary(player);

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
                SpeedBonus = sec.Speed,
                SecAttack = sec.Attack,
                SecHP = sec.HP,
                SecDefense = sec.Defense,
                SecAttackSpeed = sec.AttackSpeed,
                SecCritical = sec.Critical,
                SecCriticalDamage = sec.CriticalDamage,
                SecSpeed = sec.Speed,
                SecEfficiency = sec.Efficiency,
                SecEnergy = sec.Energy,
                LastUpdateTime = player.LastUpdateTime
            };

            await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            ThrowError(ex.Message);
        }
    }
}

