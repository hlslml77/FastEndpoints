using System.Text.Json;
using Serilog;
using Web.Data.Config;

namespace Web.Services;

public interface ITravelEventConfigService
{
    TravelEventConfig? GetById(int id);
    List<TravelEventConfig> GetAll();
}

public class TravelEventConfigService : ITravelEventConfigService
{
    private readonly List<TravelEventConfig> _events = new();

    public TravelEventConfigService()
    {
        try
        {
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Json", "Travel_EventList.json");
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var content = File.ReadAllText(jsonPath);
            var list = JsonSerializer.Deserialize<List<TravelEventConfig>>(content, opts);
            if (list != null)
                _events.AddRange(list);
            Log.Information("Loaded Travel Event configs: {Count}", _events.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load Travel_EventList.json");
            throw;
        }
    }

    public TravelEventConfig? GetById(int id) => _events.FirstOrDefault(e => e.ID == id);
    public List<TravelEventConfig> GetAll() => _events;
}

