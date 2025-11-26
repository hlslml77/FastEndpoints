using System.Text.Json;
using Web.Data.Config;
using Serilog;

namespace Web.Services;

public interface IItemConfigService
{
    ItemConfig? GetItem(int id);
    EquipmentConfig? GetEquipmentByEquipId(int equipId);
    List<ItemConfig> GetAllItems();
}

public class ItemConfigService : IItemConfigService
{
    private readonly List<ItemConfig> _items = new();
    private readonly List<EquipmentConfig> _equipments = new();

    public ItemConfigService()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Json");
        Load(basePath);
    }

    private void Load(string path)
    {
        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var itemPath = Path.Combine(path, "Item.json");
        if (File.Exists(itemPath))
        {
            var items = JsonSerializer.Deserialize<List<ItemConfig>>(File.ReadAllText(itemPath), opts);
            if (items != null) _items.AddRange(items);
        }

        var equipPath = Path.Combine(path, "Equipment.json");
        if (File.Exists(equipPath))
        {
            var equips = JsonSerializer.Deserialize<List<EquipmentConfig>>(File.ReadAllText(equipPath), opts);
            if (equips != null) _equipments.AddRange(equips);
        }
        Log.Information("Loaded {ItemCount} items and {EquipCount} equipment entries", _items.Count, _equipments.Count);
    }

    public ItemConfig? GetItem(int id) => _items.FirstOrDefault(i => i.ID == id);

    public EquipmentConfig? GetEquipmentByEquipId(int equipId)
        => _equipments.FirstOrDefault(e => e.EquipID == equipId);

    public List<ItemConfig> GetAllItems() => _items;
}

