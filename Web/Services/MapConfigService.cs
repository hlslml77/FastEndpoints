using System.Text.Json;
using Web.Data.Config;

namespace Web.Services;

/// <summary>
/// 地图配置服务 - 从 JSON 文件加载配置
/// </summary>
public interface IMapConfigService
{
    /// <summary>
    /// 根据位置ID获取地图配置
    /// </summary>
    MapBaseConfig? GetMapConfigByLocationId(int locationId);

    /// <summary>
    /// 获取所有地图配置
    /// </summary>
    List<MapBaseConfig> GetAllMapConfigs();
}

public class MapConfigService : IMapConfigService
{
    private readonly ILogger<MapConfigService> _logger;
    private readonly List<MapBaseConfig> _mapConfigs;

    public MapConfigService(ILogger<MapConfigService> logger)
    {
        _logger = logger;
        _mapConfigs = new List<MapBaseConfig>();

        try
        {
            var jsonPath = Path.Combine(AppContext.BaseDirectory, "Json", "MapBase.json");
            LoadFromJson(jsonPath);
            _logger.LogInformation("Map configuration loaded successfully from {Path}. Total configs: {Count}", 
                jsonPath, _mapConfigs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load map configuration");
            throw;
        }
    }

    private void LoadFromJson(string path)
    {
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        };

        var content = File.ReadAllText(path);
        var configs = JsonSerializer.Deserialize<List<MapBaseConfig>>(content, options);
        
        if (configs != null)
        {
            _mapConfigs.AddRange(configs);
        }
    }

    public MapBaseConfig? GetMapConfigByLocationId(int locationId)
    {
        return _mapConfigs.FirstOrDefault(c => c.LocationID == locationId);
    }

    public List<MapBaseConfig> GetAllMapConfigs()
    {
        return _mapConfigs;
    }
}

