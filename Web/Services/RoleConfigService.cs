using System.Text.Json;
using Web.Data.Config;
using Serilog;

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
    int GetExperienceFromJoules(int joules); // backward compat
    int GetExperienceFromDistance(decimal distanceMeters);

    // 运动配置：根据设备类型与距离获取四个主属性的加点结果
    SportDistributionResult? GetSportDistribution(int deviceType, decimal distance);
}

public class RoleConfigService : IRoleConfigService, IReloadableConfig, IDisposable
{
    private volatile RoleConfig _roleConfig = new();
    private volatile List<RoleAttributeDef> _attributes = new();
    private volatile List<RoleUpgradeConfig> _upgradeConfigs = new();
    private volatile List<RoleSportEntry> _sportEntries = new();
    private volatile List<RoleExperienceConfig> _experienceConfigs = new();

    private readonly string _dir;
    private readonly JsonConfigWatcher _watcher;
    public string Name => "role";
    public DateTime LastReloadTime { get; private set; }

    public RoleConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        try
        {
            Reload();
            // 监听整个 Json 目录（包含 WorldConfig.json）
            _watcher = new JsonConfigWatcher(_dir, "*.json", () => Reload());
            Log.Information("Role configuration loaded successfully from {Path}", _dir);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load role configuration from {Path}", _dir);
            throw;
        }
    }

    public void Reload()
    {
        try
        {
            LoadFromJson(_dir, out var roleCfg, out var attributes, out var upgrades, out var sports, out var exps);
            _roleConfig = roleCfg;
            _attributes = attributes;
            _upgradeConfigs = upgrades;
            _sportEntries = sports;
            _experienceConfigs = exps;
            LastReloadTime = DateTime.UtcNow;
            Log.Information("Role configs reloaded. Attributes={Attr}, Upgrades={Upg}, Sports={Sport}, Exps={Exp}",
                _attributes.Count, _upgradeConfigs.Count, _sportEntries.Count, _experienceConfigs.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload role configs failed. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new
    {
        Name,
        LastReloadTime,
        Attributes = _attributes.Count,
        Upgrades = _upgradeConfigs.Count,
        Sports = _sportEntries.Count,
        Experiences = _experienceConfigs.Count,
        Dir = _dir
    };

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void LoadFromJson(string path, out RoleConfig roleCfg, out List<RoleAttributeDef> attributes,
        out List<RoleUpgradeConfig> upgradeConfigs, out List<RoleSportEntry> sportEntries, out List<RoleExperienceConfig> experience)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        roleCfg = new RoleConfig();
        attributes = new List<RoleAttributeDef>();
        upgradeConfigs = new List<RoleUpgradeConfig>();
        sportEntries = new List<RoleSportEntry>();
        experience = new List<RoleExperienceConfig>();

        // 1) WorldConfig.json（每日属性点上限 moved here, ID=8, Value1）优先读取
        var worldCfgPath = Path.Combine(path, "WorldConfig.json");
        if (File.Exists(worldCfgPath))
        {
            try
            {
                var worldContent = File.ReadAllText(worldCfgPath);
                var root = JsonSerializer.Deserialize<JsonElement>(worldContent);
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        if (item.TryGetProperty("ID", out var idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.GetInt32() == 8)
                        {
                            if (item.TryGetProperty("Value1", out var v))
                            {
                                if (v.ValueKind == JsonValueKind.Number) roleCfg.DailyAttributePointsLimit = v.GetInt32();
                                else if (v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var lim)) roleCfg.DailyAttributePointsLimit = lim;
                            }
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to read DailyAttributePoints from WorldConfig.json; will fallback.");
            }
        }


        // 2) Role_Attribute.json（四个主属性及其每点带来的副属性加成）
        var attrPath = Path.Combine(path, "Role_Attribute.json");
        if (File.Exists(attrPath))
        {
            var attrs = JsonSerializer.Deserialize<List<RoleAttributeDef>>(File.ReadAllText(attrPath), options);
            if (attrs != null)
                attributes.AddRange(attrs.OrderBy(a => a.Id));
        }
        // 2.1) Role_AttributeID.json - 初始值来自该表（ID=1..4），字段 Init
        var attrIdPath = Path.Combine(path, "Role_AttributeID.json");
        if (File.Exists(attrIdPath) && attributes.Count > 0)
        {
            try
            {
                var root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(attrIdPath));
                if (root.ValueKind == JsonValueKind.Array)
                {
                    var initDict = new Dictionary<int, int>();
                    foreach (var item in root.EnumerateArray())
                    {
                        if (!item.TryGetProperty("ID", out var idEl)) continue;
                        var id = idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt32() : 0;
                        if (id < 1 || id > 4) continue; // 只取 1..4 主属性
                        int init = 0;
                        if (item.TryGetProperty("Init", out var initEl))
                        {
                            if (initEl.ValueKind == JsonValueKind.Number) init = initEl.GetInt32();
                            else if (initEl.ValueKind == JsonValueKind.String && int.TryParse(initEl.GetString(), out var tmp)) init = tmp;
                        }
                        initDict[id] = init;
                    }
                    if (initDict.Count > 0)
                    {
                        foreach (var def in attributes)
                        {
                            if (initDict.TryGetValue(def.Id, out var val))
                                def.Initial = val;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to merge Role_AttributeID.json inits.");
            }
        }

        // 3) Role_Upgrade.json（等级） - 兼容 ID 或 Rank 字段
        var upPath = Path.Combine(path, "Role_Upgrade.json");
        if (File.Exists(upPath))
        {
            try
            {
                var root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(upPath));
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        var cfg = new RoleUpgradeConfig();
                        // Rank 兼容
                        if (item.TryGetProperty("Rank", out var rEl) && rEl.ValueKind == JsonValueKind.Number)
                            cfg.Rank = rEl.GetInt32();
                        else if (item.TryGetProperty("ID", out var idEl3) && idEl3.ValueKind == JsonValueKind.Number)
                            cfg.Rank = idEl3.GetInt32();

                        if (item.TryGetProperty("Experience", out var expEl) && expEl.ValueKind == JsonValueKind.Number)
                            cfg.Experience = expEl.GetInt32();

                        int ReadInt(string name)
                        {
                            if (item.TryGetProperty(name, out var el))
                            {
                                if (el.ValueKind == JsonValueKind.Number) return el.GetInt32();
                                if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var t)) return t;
                            }
                            return 0;
                        }
                        cfg.UpperLimb = ReadInt("UpperLimb");
                        cfg.LowerLimb = ReadInt("LowerLimb");
                        cfg.Core = ReadInt("Core");
                        cfg.HeartLungs = ReadInt("HeartLungs");

                        upgradeConfigs.Add(cfg);
                    }
                    upgradeConfigs.Sort((a, b) => a.Rank.CompareTo(b.Rank));
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to parse Role_Upgrade.json; upgrades may be empty.");
            }
        }

        // 4) Role_Sport.json（距离与分配） - 新版为长度4数组，索引 0=跑步机,1=划船机,2=单车,3=手环
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

                    entry.UpperLimb = TryToDeviceArray(item, "UpperLimb");
                    entry.LowerLimb = TryToDeviceArray(item, "LowerLimb");
                    entry.Core = TryToDeviceArray(item, "Core");
                    entry.HeartLungs = TryToDeviceArray(item, "HeartLungs");

                    sportEntries.Add(entry);
                }
                sportEntries.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }
        }

        // 5) Role_Experience.json
        var expPath = Path.Combine(path, "Role_Experience.json");
        if (File.Exists(expPath))
        {
            var exps = JsonSerializer.Deserialize<List<RoleExperienceConfig>>(File.ReadAllText(expPath), options);
            if (exps != null)
                experience.AddRange(exps.OrderBy(e => e.Distance > 0 ? e.Distance : e.Joule));
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
    private static List<int>? TryToDeviceArray(JsonElement node, string propName)
    {
        if (!node.TryGetProperty(propName, out var prop) || prop.ValueKind == JsonValueKind.Null)
            return null;
        if (prop.ValueKind != JsonValueKind.Array)
            return null;

        // 情况A：新格式，直接是长度为4的数字数组 [跑步机, 划船机, 单车, 手环]
        bool allNumbers = true;
        foreach (var el in prop.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Number)
            {
                allNumbers = false;
                break;
            }
        }
        if (allNumbers)
        {
            var res = new int[4];
            int i = 0;
            foreach (var el in prop.EnumerateArray())
            {
                if (i < 4)
                    res[i] = el.GetInt32();
                i++;
            }
            return new List<int>(res);
        }

        // 情况B：兼容旧格式 [[deviceType, points], ...]
        var map = new int[4];
        foreach (var row in prop.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array) continue;
            int idx = 0;
            int device = -1, points = 0;
            foreach (var cell in row.EnumerateArray())
            {
                if (idx == 0)
                {
                    if (cell.ValueKind == JsonValueKind.Number) device = cell.GetInt32();
                    else if (cell.ValueKind == JsonValueKind.String && int.TryParse(cell.GetString(), out var dv)) device = dv;
                }
                else if (idx == 1)
                {
                    if (cell.ValueKind == JsonValueKind.Number) points = cell.GetInt32();
                    else if (cell.ValueKind == JsonValueKind.String && int.TryParse(cell.GetString(), out var pv)) points = pv;
                }
                idx++;
            }
            if (device >= 0 && device < 4)
            {
                // 新规则：为最终值，直接覆盖
                map[device] = points;
            }
        }
        return new List<int>(map);
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
            .Where(c => c.Joule > 0 && c.Joule <= joules)
            .OrderByDescending(c => c.Joule)
            .FirstOrDefault();
        return config?.Experience ?? 0;
    }

    public int GetExperienceFromDistance(decimal distanceMeters)
    {
        var dm = (int)Math.Floor(distanceMeters);
        var config = _experienceConfigs
            .Where(c => c.Distance > 0 && c.Distance <= dm)
            .OrderByDescending(c => c.Distance)
            .FirstOrDefault();
        if (config != null) return config.Experience;

        // fallback: 若 Distance 全为 0，则尝试按 Joule（兼容旧表）
        return GetExperienceFromJoules(dm);
    }

    public SportDistributionResult? GetSportDistribution(int deviceType, decimal distance)
    {
        // 取不超过 distance 的最大条目（距离单位与 Role_Sport.json 保持一致；当前配置为米）
        var entry = _sportEntries
            .Where(e => e.Distance <= distance)
            .OrderByDescending(e => e.Distance)
            .FirstOrDefault();
        if (entry == null) return null;

        int GetVal(List<int>? arr)
        {
            if (arr == null) return 0;
            if (deviceType < 0 || deviceType >= arr.Count) return 0;
            return arr[deviceType];
        }

        return new SportDistributionResult
        {
            UpperLimb = GetVal(entry.UpperLimb),
            LowerLimb = GetVal(entry.LowerLimb),
            Core = GetVal(entry.Core),
            HeartLungs = GetVal(entry.HeartLungs)
        };
    }
}

