using System.Text.Json;
using Web.Data.Config;
using Serilog;

namespace Web.Services;

public interface IRandomWorldEventConfigService : IReloadableConfig, IDisposable
{
    IReadOnlyList<RandomEventConfigEntry> GetEventConfigs();
    IReadOnlyList<RandomPointConfigEntry> GetRandomPointConfigs();
    RandomEventConfigEntry? GetEventById(int id);
}

public class RandomWorldEventConfigService : IRandomWorldEventConfigService
{
    private readonly string _dir;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private readonly JsonConfigWatcher _watcher;

    private volatile List<RandomEventConfigEntry> _events = new();
    private volatile List<RandomPointConfigEntry> _points = new();

    public string Name => "event";
    public DateTime LastReloadTime { get; private set; }

    public RandomWorldEventConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        _watcher = new JsonConfigWatcher(_dir, "WorldUiMap_Random*.json", () => Reload());
    }

    public void Reload()
    {
        try
        {
            var eventsPath = Path.Combine(_dir, "WorldUiMap_RandomEvent.json");
            var pointsPath = Path.Combine(_dir, "WorldUiMap_RandomPoint.json");

            if (File.Exists(eventsPath))
            {
                _events = JsonSerializer.Deserialize<List<RandomEventConfigEntry>>(File.ReadAllText(eventsPath), _opts) ?? new List<RandomEventConfigEntry>();
            }
            if (File.Exists(pointsPath))
            {
                _points = JsonSerializer.Deserialize<List<RandomPointConfigEntry>>(File.ReadAllText(pointsPath), _opts) ?? new List<RandomPointConfigEntry>();
            }

            LastReloadTime = DateTime.UtcNow;
            Log.Information("Random world event configs reloaded. Events={EventCount} Points={PointCount}", _events.Count, _points.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload random world event configs failed. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Events = _events.Count, Points = _points.Count, Dir = _dir };

    public IReadOnlyList<RandomEventConfigEntry> GetEventConfigs() => _events;
    public IReadOnlyList<RandomPointConfigEntry> GetRandomPointConfigs() => _points;

    public RandomEventConfigEntry? GetEventById(int id) => _events.FirstOrDefault(e => e.ID == id);

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}

