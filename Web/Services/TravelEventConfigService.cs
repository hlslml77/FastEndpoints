using System.Text.Json;
using Serilog;
using Web.Data.Config;

namespace Web.Services;

public interface ITravelEventConfigService
{
    TravelEventConfig? GetById(int id);
    IReadOnlyList<TravelEventConfig> GetAll();
}

public class TravelEventConfigService : ITravelEventConfigService, IReloadableConfig, IDisposable
{
    private readonly string _dir;
    private readonly JsonConfigWatcher _watcher;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private volatile List<TravelEventConfig> _events = new();

    public string Name => "event";
    public DateTime LastReloadTime { get; private set; }

    public TravelEventConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        _watcher = new JsonConfigWatcher(_dir, "Travel_EventList.json", () => Reload());
    }

    public void Reload()
    {
        try
        {
            var jsonPath = Path.Combine(_dir, "Travel_EventList.json");
            var content = File.ReadAllText(jsonPath);
            var list = JsonSerializer.Deserialize<List<TravelEventConfig>>(content, _opts) ?? new List<TravelEventConfig>();
            _events = list;
            LastReloadTime = DateTime.UtcNow;
            Log.Information("Travel events reloaded: {Count}", _events.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to reload Travel_EventList.json. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Count = _events.Count, Dir = _dir };

    public TravelEventConfig? GetById(int id) => _events.FirstOrDefault(e => e.ID == id);
    public IReadOnlyList<TravelEventConfig> GetAll() => _events;

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
