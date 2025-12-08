using System.Text.Json;
using Serilog;

namespace Web.Services;

public interface IPveRankConfigService : IReloadableConfig, IDisposable
{
    int GetWeeklySettlementDay(); // 1=Monday..7=Sunday
    int GetSeasonType(); // 1=ByYear (default), reserved

    List<(int from, int to, List<(int itemId,int amount)> rewards)> GetWeekRewards(int deviceType);
    List<(int from, int to, List<(int itemId,int amount)> rewards)> GetSeasonRewards(int deviceType);
}

public class PveRankConfigService : IPveRankConfigService
{
    private readonly string _dir;
    private readonly JsonSerializerOptions _opts = new() { PropertyNameCaseInsensitive = true };
    private readonly JsonConfigWatcher _watcher;

    private volatile List<JsonElement> _weekRewards = new();
    private volatile List<JsonElement> _seasonRewards = new();
    private volatile int _weeklyDay = 1; // Monday

    public string Name => "pverank";
    public DateTime LastReloadTime { get; private set; }

    public PveRankConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        Reload();
        _watcher = new JsonConfigWatcher(_dir, "PVERank_*.json", () => Reload());
    }

    public void Reload()
    {
        try
        {
            var cfgPath = Path.Combine(_dir, "PVERank_Config.json");
            if (File.Exists(cfgPath))
            {
                try
                {
                    var arr = JsonSerializer.Deserialize<List<RankConfigRoot>>(File.ReadAllText(cfgPath), _opts);
                    _weeklyDay = arr?.FirstOrDefault()?.WeeklySettlement is int d && d >= 1 && d <= 7 ? d : 1;
                }
                catch (Exception ex) { Log.Warning(ex, "Failed to parse PVERank_Config.json"); }
            }

            var weekPath = Path.Combine(_dir, "PVERank_WeekReward.json");
            if (File.Exists(weekPath))
            {
                _weekRewards = JsonSerializer.Deserialize<List<JsonElement>>(File.ReadAllText(weekPath), _opts) ?? new();
            }
            var seasonPath = Path.Combine(_dir, "PVERank_SeasonReward.json");
            if (File.Exists(seasonPath))
            {
                _seasonRewards = JsonSerializer.Deserialize<List<JsonElement>>(File.ReadAllText(seasonPath), _opts) ?? new();
            }

            LastReloadTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload PVERank config failed");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, WeekRewards = _weekRewards.Count, SeasonRewards = _seasonRewards.Count };

    public int GetWeeklySettlementDay() => _weeklyDay;
    public int GetSeasonType() => 1;

    private static (int from, int to) ParseRange(JsonElement e)
    {
        var arr = e.GetProperty("Ranking").EnumerateArray().Select(x => x.GetString()).ToArray();
        int from = int.Parse(arr[0]!);
        int to = int.Parse(arr.Length > 1 ? arr[1]! : arr[0]!);
        return (from, to);
    }

    private static List<(int itemId, int amount)> ParseRewards(JsonElement e, int deviceType)
    {
        // deviceType: 0=Run,1=Rowing,2=Bicycle,3=Bracelet
        string key = deviceType switch { 0 => "RunReward", 1 => "RowingReward", 2 => "BicycleReward", 3 => "BraceletReward", _ => "RunReward" };
        if (!e.TryGetProperty(key, out var arr)) return new();
        var list = new List<(int,int)>();
        foreach (var p in arr.EnumerateArray())
        {
            var a = p.EnumerateArray().ToArray();
            if (a.Length >= 2) list.Add((a[0].GetInt32(), a[1].GetInt32()));
        }
        return list;
    }

    private static List<(int from, int to, List<(int,int)> rewards)> Build(JsonElement[] elems, int deviceType)
    {
        var list = new List<(int from, int to, List<(int,int)> rewards)>();
        foreach (var e in elems)
        {
            var range = ParseRange(e);
            var rewards = ParseRewards(e, deviceType);
            if (rewards.Count > 0) list.Add((range.from, range.to, rewards));
        }
        return list.OrderBy(x => x.from).ToList();
    }

    public List<(int from, int to, List<(int itemId,int amount)> rewards)> GetWeekRewards(int deviceType)
        => Build(_weekRewards.ToArray(), deviceType);

    public List<(int from, int to, List<(int itemId,int amount)> rewards)> GetSeasonRewards(int deviceType)
        => Build(_seasonRewards.ToArray(), deviceType);

    private record RankConfigRoot(int ID, int WeeklySettlement, int SeasonSettlement);

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}

