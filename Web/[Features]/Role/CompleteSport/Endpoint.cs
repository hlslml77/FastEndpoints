using Web.Services;
using FastEndpoints;
using System.Security.Claims;
using RoleApi;
using Serilog;

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
            .WithDescription("记录玩家完成的运动，根据运动类型和距离增加对应属性。设备类型: 0=跑步, 1=划船, 2=单车, 3=手环。距离单位: 米。需要JWT token验证。"));
    }

    public override async Task HandleAsync(CompleteSportRequest req, CancellationToken ct)
    {
        long userId = 0;
        try
        {
            // 从JWT解析用户ID（优先 sub，其次 userId/NameIdentifier）
            var userIdStr = User?.Claims?.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "userId" || c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userIdStr) || !long.TryParse(userIdStr, out userId))
            {
                var errorBody = new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "未能从令牌解析用户ID" };
                await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
                return;
            }

            // 记录用户运动信息
            Log.Information("玩家 {UserId} 完成运动: 类型={DeviceType}, 距离={Distance}m", userId, req.DeviceType, req.Distance);

            var player = await _roleGrowthService.CompleteSportAsync(userId, req.DeviceType, req.Distance, req.Calorie);
            var config = _configService.GetRoleConfig();
            var nextLevelExp = _configService.GetExperienceForLevel(player.CurrentLevel);

            

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
        catch (ArgumentException ex)
        {
            Log.Warning(ex, "玩家 {UserId} 运动参数异常: {Message}", userId, ex.Message ?? "参数错误");
            var msg = ex.Message ?? string.Empty;
            var code = msg.Contains("Invalid sport distribution", StringComparison.OrdinalIgnoreCase)
                ? Web.Data.ErrorCodes.Role.InvalidSportDistribution
                : Web.Data.ErrorCodes.Common.BadRequest;
            var errorBody = new { statusCode = 400, code = code, message = ex.Message ?? "请求参数错误" };
            await HttpContext.Response.SendAsync(errorBody, 400, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CompleteSport 处理失败. UserId={UserId}", userId);
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "服务器内部错误" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }
}

