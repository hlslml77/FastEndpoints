using System.Text.Json;
using Web.Data.Config;
using Serilog;

namespace Web.Services;

/// <summary>
/// 地图配置服务 - 从 JSON 文件加载配置
/// </summary>
public interface IMapConfigService
{
    MapBaseConfig? GetMapConfigByLocationId(int locationId);
    IReadOnlyList<MapBaseConfig> GetAllMapConfigs();
}

public class MapConfigService : IMapConfigService, IReloadableConfig, IDisposable
{
    private readonly string _dir;
    private readonly JsonConfigWatcher _watcher;
    private volatile List<MapBaseConfig> _mapConfigs = new();

    public string Name => "map";
    public DateTime LastReloadTime { get; private set; }

    public MapConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        try
        {
            Reload();
            _watcher = new JsonConfigWatcher(_dir, "WorldUiMap_MapBase.json", () => Reload());
            Log.Information("Map configuration initialized from {Dir}", _dir);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize map configuration");
            throw;
        }
    }

    public void Reload()
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var path = Path.Combine(_dir, "WorldUiMap_MapBase.json");
            var content = File.ReadAllText(path);
            var configs = JsonSerializer.Deserialize<List<MapBaseConfig>>(content, options) ?? new List<MapBaseConfig>();
            _mapConfigs = configs;
            LastReloadTime = DateTime.UtcNow;
            Log.Information("Map configs reloaded. Count={Count}", _mapConfigs.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload map configs failed. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Count = _mapConfigs.Count, Dir = _dir };

    public MapBaseConfig? GetMapConfigByLocationId(int locationId) => _mapConfigs.FirstOrDefault(c => c.LocationID == locationId);
    public IReadOnlyList<MapBaseConfig> GetAllMapConfigs() => _mapConfigs;

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
