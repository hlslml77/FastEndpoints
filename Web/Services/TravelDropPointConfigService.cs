using System.Text.Json;
using Serilog;
using Web.Data.Config;

namespace Web.Services;

public interface ITravelDropPointConfigService
{
    IReadOnlyList<TravelDropPointConfig> GetByLevel(int levelId);
    IReadOnlyList<TravelDropPointConfig> GetAll();
}

public class TravelDropPointConfigService : ITravelDropPointConfigService, IReloadableConfig, IDisposable
{
    private readonly string _dir;
    private readonly JsonConfigWatcher _watcher;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private volatile List<TravelDropPointConfig> _configs = new();

    public string Name => "drop";
    public DateTime LastReloadTime { get; private set; }

    public TravelDropPointConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        _watcher = new JsonConfigWatcher(_dir, "Travel_DropPoint.json", () => Reload());
    }

    public void Reload()
    {
        try
        {
            var jsonPath = Path.Combine(_dir, "Travel_DropPoint.json");
            var content = File.ReadAllText(jsonPath);
            var list = JsonSerializer.Deserialize<List<TravelDropPointConfig>>(content, _opts) ?? new List<TravelDropPointConfig>();
            _configs = list;
            LastReloadTime = DateTime.UtcNow;
            Log.Information("Travel DropPoint reloaded: {Count}", _configs.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload Travel_DropPoint.json. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Count = _configs.Count, Dir = _dir };

    public IReadOnlyList<TravelDropPointConfig> GetByLevel(int levelId)
        => _configs.Where(c => c.LevelID == levelId).ToList();

    public IReadOnlyList<TravelDropPointConfig> GetAll() => _configs;

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
