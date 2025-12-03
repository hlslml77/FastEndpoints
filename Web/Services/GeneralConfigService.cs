using System.Text.Json;
using Web.Data.Config;
using Serilog;

namespace Web.Services;

public interface IGeneralConfigService : IReloadableConfig, IDisposable
{
    int InitialGoldItemId { get; }
    int InitialStaminaItemId { get; }

    int GetInitialGoldAmount();
    int GetInitialStaminaAmount();
    decimal GetStoredEnergyMaxMeters();

    /// <summary>
    /// 每日随机事件点位生成数量（大地图）。来自 Config.json 中描述包含“每日随机事件点位生成数量”的配置（ID 6）。
    /// 若未配置则默认 1。
    /// </summary>
    int GetDailyRandomEventCount();

    /// <summary>
    /// 读取“玩家选择大地图点位时机器人数量显示”的范围（Value4：[min,max]）。若未配置返回 (0,0)。
    /// </summary>
    (int min, int max) GetRobotDisplayRange();
}

public class GeneralConfigService : IGeneralConfigService
{
    private readonly string _dir;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private readonly JsonConfigWatcher _watcher;

    private volatile List<GeneralConfigEntry> _entries = new();

    private class GeneralSimpleConfig
    {
        public int InitialStaminaItemId { get; set; } = 1002;
        public int InitialGoldItemId { get; set; } = 1000;
        public decimal StoredEnergyMeters { get; set; } = 10000m;
    }

    private volatile GeneralSimpleConfig _simple = new();

    public string Name => "general";
    public DateTime LastReloadTime { get; private set; }

    public int InitialGoldItemId => _simple.InitialGoldItemId;
    public int InitialStaminaItemId => _simple.InitialStaminaItemId;

    public GeneralConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        // 监听两个配置文件：Config.json（数组）与 config.json（对象）
        _watcher = new JsonConfigWatcher(_dir, "*.json", () => Reload());
    }

    public void Reload()
    {
        try
        {
            // 1) 读取简单对象 config.json（若存在）
            var simplePath = Path.Combine(_dir, "config.json");
            if (File.Exists(simplePath))
            {
                var simple = JsonSerializer.Deserialize<GeneralSimpleConfig>(File.ReadAllText(simplePath), _opts);
                if (simple != null) _simple = simple;
            }

            // 2) 读取数组 Config.json（若存在）
            var arrayPath = Path.Combine(_dir, "Config.json");
            if (File.Exists(arrayPath))
            {
                var entries = JsonSerializer.Deserialize<List<GeneralConfigEntry>>(File.ReadAllText(arrayPath), _opts) ?? new List<GeneralConfigEntry>();
                _entries = entries;
            }

            LastReloadTime = DateTime.UtcNow;
            Log.Information("General config reloaded. Simple:{HasSimple} Entries:{Count}", File.Exists(simplePath), _entries.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload general config failed. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Count = _entries.Count, Dir = _dir, Simple = _simple };

    private GeneralConfigEntry? FindByIdOrDesc(int id, string descContains)
        => _entries.FirstOrDefault(e => e.ID == id) ?? _entries.FirstOrDefault(e => (e.Desc ?? string.Empty).Contains(descContains, StringComparison.OrdinalIgnoreCase));

    public int GetInitialGoldAmount()
    {
        var e = FindByIdOrDesc(1, "金币");
        return e?.Value1 ?? 0;
    }

    public int GetInitialStaminaAmount()
    {
        var e = FindByIdOrDesc(2, "体力");
        return e?.Value1 ?? 0;
    }

    public decimal GetStoredEnergyMaxMeters()
    {
        if (_simple.StoredEnergyMeters > 0) return _simple.StoredEnergyMeters;
        var e = FindByIdOrDesc(4, "能量槽");
        var km = (decimal)(e?.Value1 ?? 10); // 默认10km
        return km * 1000m;
    }

    public int GetDailyRandomEventCount()
    {
        var e = FindByIdOrDesc(6, "每日随机事件点位生成数量");
        var count = e?.Value1 ?? 1;
        return count <= 0 ? 1 : count;
    }

    public (int min, int max) GetRobotDisplayRange()
    {
        var e = FindByIdOrDesc(5, "机器人数量显示");
        var arr = e?.Value4;
        if (arr is { Count: 2 })
        {
            var min = arr[0];
            var max = arr[1];
            if (max < min) (min, max) = (max, min);
            return (min, max);
        }
        return (0, 0);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
