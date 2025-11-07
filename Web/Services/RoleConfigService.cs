using System.Text.Json;
using System.Text.Json.Nodes;
using Web.Data.Config;

namespace Web.Services;

/// <summary>
/// 角色配置服务 - 从 JSON 文件加载配置
/// </summary>
public interface IRoleConfigService
{
    RoleConfig GetRoleConfig();
    RoleUpgradeConfig? GetUpgradeConfig(int level);
    int GetExperienceForLevel(int level);
    int GetExperienceFromJoules(int joules);
    RoleSportConfig? GetSportConfig(int deviceType, decimal distance);
    List<RoleUpgradeConfig> GetAllUpgradeConfigs();
}

public class RoleConfigService : IRoleConfigService
{
    private readonly RoleConfig _roleConfig;
    private readonly List<RoleUpgradeConfig> _upgradeConfigs;
    private readonly List<RoleSportConfig> _sportConfigs;
    private readonly List<RoleExperienceConfig> _experienceConfigs;
    private readonly ILogger<RoleConfigService> _logger;

    public RoleConfigService(ILogger<RoleConfigService> logger)
    {
        _logger = logger;
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Json");

        _roleConfig = new RoleConfig();
        _upgradeConfigs = new List<RoleUpgradeConfig>();
        _sportConfigs = new List<RoleSportConfig>();
        _experienceConfigs = new List<RoleExperienceConfig>();

        try
        {
            LoadFromJson(jsonPath);
            _logger.LogInformation("Role configuration loaded successfully from {Path}", jsonPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load role configuration from {Path}", jsonPath);
            throw;
        }
    }

    private void LoadFromJson(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        // 加载 RoleCommon.json
        var commonContent = File.ReadAllText(Path.Combine(path, "RoleCommon.json"));
        var commonNodes = JsonSerializer.Deserialize<JsonNode[]>(commonContent);
        if (commonNodes != null)
        {
            // 初始属性
            var initialAttrs = commonNodes[0]?["Value2"]?.AsArray();
            if (initialAttrs != null && initialAttrs.Count == 4)
            {
                _roleConfig.InitialUpperLimb = (int)initialAttrs[0]!;
                _roleConfig.InitialLowerLimb = (int)initialAttrs[1]!;
                _roleConfig.InitialCore = (int)initialAttrs[2]!;
                _roleConfig.InitialHeartLungs = (int)initialAttrs[3]!;
            }

            // 速度加成
            var speedBonus = commonNodes[1]?["Value3"]?.AsArray();
            if (speedBonus != null && speedBonus.Count == 4)
            {
                _roleConfig.UpperLimbSpeedBonus = (decimal)speedBonus[0]!;
                _roleConfig.LowerLimbSpeedBonus = (decimal)speedBonus[1]!;
                _roleConfig.CoreSpeedBonus = (decimal)speedBonus[2]!;
                _roleConfig.HeartLungsSpeedBonus = (decimal)speedBonus[3]!;
            }

            // 每日上限
            _roleConfig.DailyAttributePointsLimit = (int?)commonNodes[2]?["Value1"] ?? 0;
        }

        // 加载 RoleUpgrade.json
        var upgradeContent = File.ReadAllText(Path.Combine(path, "RoleUpgrade.json"));
        _upgradeConfigs.AddRange(JsonSerializer.Deserialize<List<RoleUpgradeConfig>>(upgradeContent, options) ?? new List<RoleUpgradeConfig>());

        // 加载 RoleSport.json
        var sportContent = File.ReadAllText(Path.Combine(path, "RoleSport.json"));
        _sportConfigs.AddRange(JsonSerializer.Deserialize<List<RoleSportConfig>>(sportContent, options) ?? new List<RoleSportConfig>());

        // 加载 RoleExperience.json
        var experienceContent = File.ReadAllText(Path.Combine(path, "RoleExperience.json"));
        _experienceConfigs.AddRange(JsonSerializer.Deserialize<List<RoleExperienceConfig>>(experienceContent, options) ?? new List<RoleExperienceConfig>());
    }

    public RoleConfig GetRoleConfig() => _roleConfig;

    public RoleUpgradeConfig? GetUpgradeConfig(int level)
    {
        return _upgradeConfigs.FirstOrDefault(c => c.Id == level);
    }

    public int GetExperienceForLevel(int level)
    {
        var config = GetUpgradeConfig(level);
        return config?.Experience ?? 0;
    }

    public int GetExperienceFromJoules(int joules)
    {
        var config = _experienceConfigs
            .Where(c => c.Joule <= joules)
            .OrderByDescending(c => c.Joule)
            .FirstOrDefault();

        return config?.Experience ?? 0;
    }

    public RoleSportConfig? GetSportConfig(int deviceType, decimal distance)
    {
        return deviceType switch
        {
            1 => _sportConfigs
                .Where(c => c.Distance <= distance && c.BicycleLowerLimb > 0)
                .OrderByDescending(c => c.Distance)
                .FirstOrDefault(),
            2 => _sportConfigs
                .Where(c => c.Distance <= distance && c.RunHeartLungs > 0)
                .OrderByDescending(c => c.Distance)
                .FirstOrDefault(),
            3 => _sportConfigs
                .Where(c => c.Distance <= distance && c.RowingUpperLimb > 0)
                .OrderByDescending(c => c.Distance)
                .FirstOrDefault(),
            _ => null
        };
    }

    public List<RoleUpgradeConfig> GetAllUpgradeConfigs() => _upgradeConfigs;
}

