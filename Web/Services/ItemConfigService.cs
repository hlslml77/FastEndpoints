using System.Text.Json;
using Web.Data.Config;
using Serilog;


namespace Web.Services;

public interface IItemConfigService
{
    ItemConfig? GetItem(int id);
    EquipmentConfig? GetEquipmentByEquipId(int equipId);
    EquipmentRandomConfig? GetRandomConfig(int randomGroup);
    IReadOnlyList<EquipmentEntryConfig> GetAllEntryConfigs();
    IReadOnlyList<ItemConfig> GetAllItems();
}

public class ItemConfigService : IItemConfigService, IReloadableConfig, IDisposable
{
    private readonly string _dir;
private volatile List<EquipmentRandomConfig> _randomConfigs = new();
    private volatile List<EquipmentEntryConfig> _entryConfigs = new();
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString };
    private readonly JsonConfigWatcher _watcher;

    private volatile List<ItemConfig> _items = new();
    private volatile List<EquipmentConfig> _equipments = new(); // cache of equipment configs

    public string Name => "item";
    public DateTime LastReloadTime { get; private set; }

    public ItemConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        _watcher = new JsonConfigWatcher(_dir, "*.json", () => Reload());
    }

public EquipmentRandomConfig? GetRandomConfig(int randomGroup) => _randomConfigs.FirstOrDefault(r => r.RandomGroup == randomGroup);
    public IReadOnlyList<EquipmentEntryConfig> GetAllEntryConfigs() => _entryConfigs;
    public void Reload()
    {
        var newItems = new List<ItemConfig>();
var newRandoms = new List<EquipmentRandomConfig>();
            var newEntries = new List<EquipmentEntryConfig>();
        var newEquips = new List<EquipmentConfig>();
        try
        {
            var itemPath = Path.Combine(_dir, "Item.json");
            if (File.Exists(itemPath))
            {
                var items = JsonSerializer.Deserialize<List<ItemConfig>>(File.ReadAllText(itemPath), _opts);
                if (items != null) newItems.AddRange(items);
var randomPath = Path.Combine(_dir, "Equipment-Random.json");
                if (File.Exists(randomPath))
                {
                    var rands = JsonSerializer.Deserialize<List<EquipmentRandomConfig>>(File.ReadAllText(randomPath), _opts);
                    if (rands != null) newRandoms.AddRange(rands);
                }

                var entryPath = Path.Combine(_dir, "EquipmentEntry.json");
                if (File.Exists(entryPath))
                {
                    var entries = JsonSerializer.Deserialize<List<EquipmentEntryConfig>>(File.ReadAllText(entryPath), _opts);
                    if (entries != null) newEntries.AddRange(entries);
                }
            }

            var equipPath = Path.Combine(_dir, "Equipment.json");
            if (File.Exists(equipPath))
            {
                var equips = JsonSerializer.Deserialize<List<EquipmentConfig>>(File.ReadAllText(equipPath), _opts);
                if (equips != null) newEquips.AddRange(equips);
            }

            _items = newItems;
_randomConfigs = newRandoms;
            _entryConfigs = newEntries;
            _equipments = newEquips;
            LastReloadTime = DateTime.UtcNow;
            Log.Information("Item configs reloaded. Items={ItemCount}, Equipments={EquipCount}", _items.Count, _equipments.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload Item/Equipment config failed. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Items = _items.Count, Equipments = _equipments.Count, Dir = _dir };

    public ItemConfig? GetItem(int id) => _items.FirstOrDefault(i => i.ID == id);
    public EquipmentConfig? GetEquipmentByEquipId(int equipId) => _equipments.FirstOrDefault(e => e.ID == equipId);
    public IReadOnlyList<ItemConfig> GetAllItems() => _items;

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}
