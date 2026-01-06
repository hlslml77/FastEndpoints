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
    /// 每日随机事件点位生成数量（大地图）。来自 WorldConfig.json 中描述包含“每日随机事件点位生成数量”的配置（ID 6）。
    /// 若未配置则默认 1。
    /// </summary>
    int GetDailyRandomEventCount();

    /// <summary>
    /// 读取“玩家选择大地图点位时机器人数量显示”的范围（Value4：[min,max]）。若未配置返回 (0,0)。
    /// </summary>
    (int min, int max) GetRobotDisplayRange();

    /// <summary>
    /// 体力上限（WorldConfig ID=14）。未配置则默认为 120。
    /// </summary>
    int GetStaminaMax();

    /// <summary>
    /// 体力恢复间隔（分钟）（WorldConfig ID=15）。未配置则默认为 30 分钟。
    /// </summary>
    int GetStaminaRecoverIntervalMinutes();

        /// <summary>
        /// 玩家初始点（WorldConfig.json ID=7 的 Value1）。未配置或非法时返回 0。
        /// </summary>
        int GetInitialLocationId();

        /// <summary>
        /// 藏品抽取/合成消耗的碎片数量（WorldConfig.json ID=16 的 Value1）。未配置或非法时返回 3。
        /// </summary>
        int GetCollectionObtainCost();

}

public class GeneralConfigService : IGeneralConfigService
{
    private readonly string _dir;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private readonly JsonConfigWatcher _watcher;

    private volatile List<GeneralConfigEntry> _entries = new();

    public string Name => "general";
    public DateTime LastReloadTime { get; private set; }

    public int InitialGoldItemId => 1000;  // 默认金币物品ID
    public int InitialStaminaItemId => 1002;  // 默认体力物品ID

    public GeneralConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        // 监听 JSON 目录变更，发现变动时重载（WorldConfig.json 等）
        _watcher = new JsonConfigWatcher(_dir, "*.json", () => Reload());
    }

    public void Reload()
    {
        try
        {
            // 读取数组 WorldConfig.json（若存在）
            var arrayPath = Path.Combine(_dir, "WorldConfig.json");
            if (File.Exists(arrayPath))
            {
                try
                {
                    var entries = JsonSerializer.Deserialize<List<GeneralConfigEntry>>(File.ReadAllText(arrayPath), _opts) ?? new List<GeneralConfigEntry>();
                    _entries = entries;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to load WorldConfig.json, keeping previous entries");
                }
            }

            LastReloadTime = DateTime.UtcNow;
            Log.Information("General config reloaded. Entries:{Count}", _entries.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload general config failed. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Count = _entries.Count, Dir = _dir };

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
        var e = FindByIdOrDesc(4, "能量槽");
        // WorldConfig.json 中 ID=4 的 Value1 单位已改为“米”（m），默认 10000 米
        var meters = (decimal)(e?.Value1 ?? 10000);
        return meters;
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

    /// <summary>
    /// 体力上限（WorldConfig ID=14）。未配置则默认为 120。
    /// </summary>
    public int GetStaminaMax()
    {
        var e = FindByIdOrDesc(14, "体力上限");
        var val = e?.Value1 ?? 120;
        return val > 0 ? val : 120;
    }

    /// <summary>
    /// 体力恢复间隔（分钟）（WorldConfig ID=15）。未配置则默认为 30 分钟。
    /// </summary>
    public int GetStaminaRecoverIntervalMinutes()
    {
        var e = FindByIdOrDesc(15, "回复1点体力");
        var val = e?.Value1 ?? 30;
        return val > 0 ? val : 30;
    }

    public int GetInitialLocationId()
    {
        var e = FindByIdOrDesc(7, "玩家初始点");
        var id = e?.Value1 ?? 0;
        return id > 0 ? id : 0;
    }

    public int GetCollectionObtainCost()
    {
        var e = FindByIdOrDesc(16, "碎片合成");
        var cost = e?.Value1 ?? 3;
        return cost > 0 ? cost : 3;
    }

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
