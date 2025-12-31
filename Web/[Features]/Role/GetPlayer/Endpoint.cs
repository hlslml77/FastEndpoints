using Web.Services;
using FastEndpoints;
using System.Security.Claims;
using Serilog;

namespace RoleApi.GetPlayer;

/// <summary>
/// 获取玩家角色信息端点
/// </summary>
public class Endpoint : Endpoint<EmptyRequest, PlayerRoleResponse>
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
        Post("/role/get-player");
        // 需要JWT token验证，要求web_access权限
        Permissions("web_access");
        Description(x => x
            .WithTags("Role")
            .WithSummary("获取玩家角色信息")
            .WithDescription("获取指定用户的角色信息，包括等级、经验、属性等。需要JWT token验证。"));
    }

    public override async Task HandleAsync(EmptyRequest req, CancellationToken ct)
    {
        try
        {
            // 从JWT解析用户ID（优先 sub，其次 userId/NameIdentifier）
            var userIdStr = User?.Claims?.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out var userId))
            {
                var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            var player = await _roleGrowthService.GetOrCreatePlayerAsync(userId);
            var config = _configService.GetRoleConfig();
            var nextLevelExp = _configService.GetExperienceForLevel(player.CurrentLevel);

            // 即时计算九项副属性（不落库）
            var sec = _roleGrowthService.ComputeSecondary(player);

            var attributes = new List<Web.Data.PlayerAttributeType>
            {
                new() { Type = 1,   Value = (double)player.AttrUpperLimb },   // UpperLimb
                new() { Type = 2,   Value = (double)player.AttrLowerLimb },   // LowerLimb
                new() { Type = 3,   Value = (double)player.AttrCore },        // CoreRange
                new() { Type = 4,   Value = (double)player.AttrHeartLungs }   // HeartLungs
            };

            var response = new PlayerRoleResponse
            {
                UserId = player.UserId,
                CurrentLevel = player.CurrentLevel,
                CurrentExperience = player.CurrentExperience,
                ExperienceToNextLevel = nextLevelExp,
                TodayAttributePoints = player.TodayAttributePoints,
                AvailableAttributePoints = config.DailyAttributePointsLimit - player.TodayAttributePoints,
                LastUpdateTime = player.LastUpdateTime,
                Attributes = attributes
            };

            await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Get player failed");
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

