using System.Text.Json;
using Serilog;
using Web.Data.Config;

namespace Web.Services;

public interface IMonsterConfigService
{
    MonsterConfig? GetById(int id);
    IReadOnlyList<MonsterConfig> GetAll();
}

public class MonsterConfigService : IMonsterConfigService, IReloadableConfig, IDisposable
{
    private readonly string _dir;
    private readonly JsonConfigWatcher _watcher;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private volatile List<MonsterConfig> _monsters = new();

    public string Name => "monster";
    public DateTime LastReloadTime { get; private set; }

    public MonsterConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        _watcher = new JsonConfigWatcher(_dir, "Monster.json", () => Reload());
    }

    public void Reload()
    {
        try
        {
            var jsonPath = Path.Combine(_dir, "Monster.json");
            var content = File.ReadAllText(jsonPath);
            var list = JsonSerializer.Deserialize<List<MonsterConfig>>(content, _opts) ?? new List<MonsterConfig>();
            _monsters = list;
            LastReloadTime = DateTime.UtcNow;
            Log.Information("Monster configs reloaded: {Count}", _monsters.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload Monster.json. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Count = _monsters.Count, Dir = _dir };

    public MonsterConfig? GetById(int id) => _monsters.FirstOrDefault(m => m.ID == id);
    public IReadOnlyList<MonsterConfig> GetAll() => _monsters;

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}

