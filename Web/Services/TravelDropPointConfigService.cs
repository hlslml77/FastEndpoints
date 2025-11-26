using System.Text.Json;
using Serilog;
using Web.Data.Config;

namespace Web.Services;

public interface ITravelDropPointConfigService
{
    List<TravelDropPointConfig> GetByLevel(int levelId);
    List<TravelDropPointConfig> GetAll();
}

public class TravelDropPointConfigService : ITravelDropPointConfigService
{
    private readonly List<TravelDropPointConfig> _configs = new();

    public TravelDropPointConfigService()
    {
        try
        {
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Json", "Travel_DropPoint.json");
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var content = File.ReadAllText(jsonPath);
            var list = JsonSerializer.Deserialize<List<TravelDropPointConfig>>(content, opts);
            if (list != null)
                _configs.AddRange(list);
            Log.Information("Loaded Travel DropPoint configs: {Count}", _configs.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load Travel_DropPoint.json");
            throw;
        }
    }

    public List<TravelDropPointConfig> GetByLevel(int levelId)
        => _configs.Where(c => c.LevelID == levelId).ToList();

    public List<TravelDropPointConfig> GetAll() => _configs;
}

