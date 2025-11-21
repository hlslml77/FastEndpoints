using System.Text.Json;
using Web.Data.Config;

namespace Web.Services;

/// <summary>
/// 角色配置服务 - 从 JSON 文件加载配置（role_ 前缀）
/// </summary>
public interface IRoleConfigService
{
    // 全局配置
    RoleConfig GetRoleConfig();

    // 主属性定义（含初始值与每点带来的副属性加成）
    IReadOnlyList<RoleAttributeDef> GetAttributeDefs();

    // 升级配置
    RoleUpgradeConfig? GetUpgradeConfig(int level /* Rank */);
    List<RoleUpgradeConfig> GetAllUpgradeConfigs();

    // 经验配置
    int GetExperienceForLevel(int level /* Rank */);
    int GetExperienceFromJoules(int joules);

    // 运动配置：根据设备类型与距离获取四个主属性的加点结果
    SportDistributionResult? GetSportDistribution(int deviceType, decimal distance);
}

public class RoleConfigService : IRoleConfigService
{
    private readonly RoleConfig _roleConfig = new();
    private readonly List<RoleAttributeDef> _attributes = new();
    private readonly List<RoleUpgradeConfig> _upgradeConfigs = new();
    private readonly List<RoleSportEntry> _sportEntries = new();
    private readonly List<RoleExperienceConfig> _experienceConfigs = new();
    private readonly ILogger<RoleConfigService> _logger;

    public RoleConfigService(ILogger<RoleConfigService> logger)
    {
        _logger = logger;
        var jsonPath = Path.Combine(AppContext.BaseDirectory, "Json");

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

        // 1) Role_Config.json（每日属性点上限等）
        var cfgPath = Path.Combine(path, "Role_Config.json");
        if (File.Exists(cfgPath))
        {
            var cfgContent = File.ReadAllText(cfgPath);
            var root = JsonSerializer.Deserialize<JsonElement>(cfgContent);
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var id = 0;
                    if (item.TryGetProperty("ID", out var idEl))
                    {
                        if (idEl.ValueKind == JsonValueKind.Number) id = idEl.GetInt32();
                        else if (idEl.ValueKind == JsonValueKind.String && int.TryParse(idEl.GetString(), out var tmp)) id = tmp;
                    }
                    else if (item.TryGetProperty("Id", out var idEl2))
                    {
                        if (idEl2.ValueKind == JsonValueKind.Number) id = idEl2.GetInt32();
                        else if (idEl2.ValueKind == JsonValueKind.String && int.TryParse(idEl2.GetString(), out var tmp2)) id = tmp2;
                    }

                    if (id == 1)
                    {
                        if (item.TryGetProperty("Value1", out var v))
                        {
                            if (v.ValueKind == JsonValueKind.Number) _roleConfig.DailyAttributePointsLimit = v.GetInt32();
                            else if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var lim)) _roleConfig.DailyAttributePointsLimit = lim;
                        }
                        break;
                    }
                }
            }
        }

        // 2) Role_Attribute.json（四个主属性及其每点带来的副属性加成）
        var attrPath = Path.Combine(path, "Role_Attribute.json");
        if (File.Exists(attrPath))
        {
            var attrs = JsonSerializer.Deserialize<List<RoleAttributeDef>>(File.ReadAllText(attrPath), options);
            if (attrs != null)
                _attributes.AddRange(attrs.OrderBy(a => a.Id));
        }

        // 3) Role_Upgrade.json（等级=Rank）
        var upPath = Path.Combine(path, "Role_Upgrade.json");
        if (File.Exists(upPath))
        {
            var ups = JsonSerializer.Deserialize<List<RoleUpgradeConfig>>(File.ReadAllText(upPath), options);
            if (ups != null)
                _upgradeConfigs.AddRange(ups.OrderBy(u => u.Rank));
        }

        // 4) Role_Sport.json（距离与分配）
        var sportPath = Path.Combine(path, "Role_Sport.json");
        if (File.Exists(sportPath))
        {
            var content = File.ReadAllText(sportPath);
            var root = JsonSerializer.Deserialize<JsonElement>(content);
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in root.EnumerateArray())
                {
                    var entry = new RoleSportEntry();
                    if (item.TryGetProperty("ID", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                        entry.ID = idEl.GetInt32();

                    if (item.TryGetProperty("Distance", out var dEl))
                    {
                        if (dEl.ValueKind == JsonValueKind.String)
                            entry.Distance = Convert.ToDecimal(dEl.GetString(), System.Globalization.CultureInfo.InvariantCulture);
                        else if (dEl.ValueKind == JsonValueKind.Number)
                            entry.Distance = dEl.GetDecimal();
                    }

                    entry.UpperLimb = TryToMatrix(item, "UpperLimb");
                    entry.LowerLimb = TryToMatrix(item, "LowerLimb");
                    entry.Core = TryToMatrix(item, "Core");
                    entry.HeartLungs = TryToMatrix(item, "HeartLungs");

                    _sportEntries.Add(entry);
                }
                _sportEntries.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }
        }

        // 5) Role_Experience.json
        var expPath = Path.Combine(path, "Role_Experience.json");
        if (File.Exists(expPath))
        {
            var exps = JsonSerializer.Deserialize<List<RoleExperienceConfig>>(File.ReadAllText(expPath), options);
            if (exps != null)
                _experienceConfigs.AddRange(exps.OrderBy(e => e.Joule));
        }
    }

    private static List<List<int>>? TryToMatrix(JsonElement node, string propName)
    {
        if (!node.TryGetProperty(propName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return null;

        var result = new List<List<int>>();
        foreach (var row in prop.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array) continue;
            int deviceType = 0, points = 0, idx = 0;
            foreach (var cell in row.EnumerateArray())
            {
                if (idx == 0)
                {
                    if (cell.ValueKind == JsonValueKind.Number) deviceType = cell.GetInt32();
                    else if (cell.ValueKind == JsonValueKind.String && int.TryParse(cell.GetString(), out var v)) deviceType = v;
                }
                else if (idx == 1)
                {
                    if (cell.ValueKind == JsonValueKind.Number) points = cell.GetInt32();
                    else if (cell.ValueKind == JsonValueKind.String && int.TryParse(cell.GetString(), out var v2)) points = v2;
                }
                idx++;
            }
            if (idx >= 2)
                result.Add(new List<int> { deviceType, points });
        }
        return result.Count == 0 ? null : result;
    }

    public RoleConfig GetRoleConfig() => _roleConfig;
    public IReadOnlyList<RoleAttributeDef> GetAttributeDefs() => _attributes;

    public RoleUpgradeConfig? GetUpgradeConfig(int level)
    {
        return _upgradeConfigs.FirstOrDefault(c => c.Rank == level);
    }

    public List<RoleUpgradeConfig> GetAllUpgradeConfigs() => _upgradeConfigs;

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

    public SportDistributionResult? GetSportDistribution(int deviceType, decimal distance)
    {
        // 取不超过 distance 的最大条目
        var entry = _sportEntries
            .Where(e => e.Distance <= distance)
            .OrderByDescending(e => e.Distance)
            .FirstOrDefault();
        if (entry == null) return null;

        // 累计四个主属性在指定设备类型下的加点
        int SumPoints(List<List<int>>? matrix)
        {
            if (matrix == null) return 0;
            var total = 0;
            foreach (var row in matrix)
            {
                if (row.Count >= 2 && row[0] == deviceType)
                    total += row[1];
            }
            return total;
        }

        return new SportDistributionResult
        {
            UpperLimb = SumPoints(entry.UpperLimb),
            LowerLimb = SumPoints(entry.LowerLimb),
            Core = SumPoints(entry.Core),
            HeartLungs = SumPoints(entry.HeartLungs)
        };
    }
}

