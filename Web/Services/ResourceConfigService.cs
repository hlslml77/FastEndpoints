using System.Text.Json;
using Web.Data.Config;
using Serilog;

namespace Web.Services;

/// <summary>
/// 资源配置服务 - 从 WorldUiMap_Resources.json 加载配置
/// </summary>
public interface IResourceConfigService
{
    /// <summary>
    /// 根据Resources ID获取刷新时间（小时）
    /// </summary>
    int? GetRefreshTimeByResourceId(int resourceId);

    /// <summary>
    /// 获取所有资源配置
    /// </summary>
    IReadOnlyList<ResourceConfig> GetAllResourceConfigs();
}

public class ResourceConfigService : IResourceConfigService, IReloadableConfig, IDisposable
{
    private readonly string _dir;
    private readonly JsonConfigWatcher _watcher;
    private volatile List<ResourceConfig> _resourceConfigs = new();

    public string Name => "resource";
    public DateTime LastReloadTime { get; private set; }

    public ResourceConfigService()
    {
        _dir = Path.Combine(AppContext.BaseDirectory, "Json");
        try
        {
            Reload();
            _watcher = new JsonConfigWatcher(_dir, "WorldUiMap_Resources.json", () => Reload());
            Log.Information("Resource configuration initialized from {Dir}", _dir);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize resource configuration");
            throw;
        }
    }

    public void Reload()
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var path = Path.Combine(_dir, "WorldUiMap_Resources.json");
            var content = File.ReadAllText(path);
            var configs = JsonSerializer.Deserialize<List<ResourceConfig>>(content, options) ?? new List<ResourceConfig>();
            _resourceConfigs = configs;
            LastReloadTime = DateTime.UtcNow;
            Log.Information("Resource configs reloaded. Count={Count}", _resourceConfigs.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Reload resource configs failed. Keep previous snapshot.");
        }
    }

    public object GetStatus() => new { Name, LastReloadTime, Count = _resourceConfigs.Count, Dir = _dir };

    public int? GetRefreshTimeByResourceId(int resourceId)
    {
        var config = _resourceConfigs.FirstOrDefault(c => c.Resources == resourceId);
        return config?.RefreshTime;
    }

    public IReadOnlyList<ResourceConfig> GetAllResourceConfigs() => _resourceConfigs;

    public void Dispose()
    {
        _watcher.Dispose();
        GC.SuppressFinalize(this);
    }
}

