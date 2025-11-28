using System.Text.Json;
using Serilog;
using Web.Data.Config;

namespace Web.Services;

public interface ICollectionConfigService
{
    IReadOnlyList<CollectionItemCfg> Items { get; }
    IReadOnlyList<CollectionComboCfg> Combos { get; }
}

public class CollectionConfigService : ICollectionConfigService, IReloadableConfig, IDisposable
{
    private readonly string _dir;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private readonly JsonConfigWatcher _watcher;

    private volatile List<CollectionItemCfg> _items = new();
    private volatile List<CollectionComboCfg> _combos = new();

    public string Name => "collection";
    public DateTime LastReloadTime { get; private set; }

    public IReadOnlyList<CollectionItemCfg> Items => _items;
    public IReadOnlyList<CollectionComboCfg> Combos => _combos;

    public CollectionConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        _watcher = new JsonConfigWatcher(_dir, "CollectionList_*.json", () => Reload());
    }

    public void Reload()
    {
        try
        {
            var itemsPath = Path.Combine(_dir, "CollectionList_item.json");
            if (File.Exists(itemsPath))
            {
                var items = JsonSerializer.Deserialize<List<CollectionItemCfg>>(File.ReadAllText(itemsPath), _opts) ?? new();
                _items = items;
            }
            var combosPath = Path.Combine(_dir, "CollectionList_Combination.json");
            if (File.Exists(combosPath))
            {
                var combos = JsonSerializer.Deserialize<List<CollectionComboCfg>>(File.ReadAllText(combosPath), _opts) ?? new();
                _combos = combos;
            }
            LastReloadTime = DateTime.UtcNow;
            Log.Information("Collection configs reloaded. Items={ItemCount}, Combos={ComboCount}", _items.Count, _combos.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload collection configs failed. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Items = _items.Count, Combos = _combos.Count, Dir = _dir };

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}

