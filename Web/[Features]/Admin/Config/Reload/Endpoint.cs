using FastEndpoints;
using Serilog;
using Web.Auth;
using Web.Services;

namespace Admin.Config.Reload;

public class Request
{
    public string? Type { get; set; } // role|item|map|event|drop|all
}

public class Endpoint : EndpointWithoutRequest<object>
{
    private readonly IEnumerable<IReloadableConfig> _configs;

    public Endpoint(IEnumerable<IReloadableConfig> configs)
    {
        _configs = configs;
    }

    public override void Configure()
    {
        Post("admin/config/reload");
        Get("admin/config/reload"); // allow GET for convenience/testing

        Roles(Role.Admin);
        Description(x => x.WithTags("Admin", "Config").WithSummary("手动重载配置").WithDescription("按类型或全部重载 JSON 配置"));
    }

    private sealed class FileReloadRequest
    {
        public string? File { get; set; } // 单个文件（备用）
        public List<string>? Files { get; set; } // 多个文件
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 1) 收集文件名（优先 query，其次 body）
        var fileParams = HttpContext.Request.Query["file"]; // 支持 ?file=Item.json&file=Role_Upgrade.json 或逗号分隔
        var fileList = new List<string>();
        if (fileParams.Count > 0)
        {
            foreach (var fp in fileParams)
            {
                if (string.IsNullOrWhiteSpace(fp)) continue;
                foreach (var part in fp.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    fileList.Add(part);
            }
        }
        else if (HttpContext.Request.HasJsonContentType())
        {
            try
            {
                var dto = await HttpContext.Request.ReadFromJsonAsync<FileReloadRequest>(cancellationToken: ct);
                if (!string.IsNullOrWhiteSpace(dto?.File)) fileList.Add(dto.File);
                if (dto?.Files is not null) fileList.AddRange(dto.Files.Where(s => !string.IsNullOrWhiteSpace(s))!);
            }
            catch { /* ignore bad body */ }
        }

        // 标准化（仅文件名，不含路径；忽略大小写）
        fileList = fileList
            .Select(s => System.IO.Path.GetFileName(s).Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();

        // 2) 文件名 -> 配置服务 映射
        var itemFiles = new HashSet<string>(new[] { "item.json", "equipment.json" }, StringComparer.OrdinalIgnoreCase);
        var roleFiles = new HashSet<string>(new[] { "role_config.json", "role_attribute.json", "role_upgrade.json", "role_sport.json", "role_experience.json" }, StringComparer.OrdinalIgnoreCase);
        var mapFiles = new HashSet<string>(new[] { "worlduimap_mapbase.json" }, StringComparer.OrdinalIgnoreCase);
        var eventFiles = new HashSet<string>(new[] { "travel_eventlist.json" }, StringComparer.OrdinalIgnoreCase);
        var dropFiles = new HashSet<string>(new[] { "travel_droppoint.json" }, StringComparer.OrdinalIgnoreCase);

        var matchedServiceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unknownFiles = new List<string>();

        if (fileList.Count == 0)
        {
            // 不传文件 => 重载全部
            foreach (var c in _configs) matchedServiceNames.Add(c.Name);
        }
        else
        {
            foreach (var f in fileList)
            {
                if (itemFiles.Contains(f)) matchedServiceNames.Add("item");
                else if (roleFiles.Contains(f)) matchedServiceNames.Add("role");
                else if (mapFiles.Contains(f)) matchedServiceNames.Add("map");
                else if (eventFiles.Contains(f)) matchedServiceNames.Add("event");
                else if (dropFiles.Contains(f)) matchedServiceNames.Add("drop");
                else unknownFiles.Add(f);
            }
        }

        var targets = _configs.Where(c => matchedServiceNames.Contains(c.Name)).ToList();

        int ok = 0, fail = 0;
        var results = new List<object>();
        foreach (var c in targets)
        {
            try
            {
                c.Reload();
                ok++;
                results.Add(new { name = c.Name, status = "ok", c.LastReloadTime });
            }
            catch (Exception ex)
            {
                fail++;
                Log.Error(ex, "Manual reload failed for {Name}", c.Name);
                results.Add(new { name = c.Name, status = "error", error = ex.Message });
            }
        }

        foreach (var uf in unknownFiles)
            results.Add(new { name = (string?)null, status = "ignored", file = uf });

        await HttpContext.Response.SendAsync(new { requestedFiles = fileList, services = matchedServiceNames.ToArray(), ok, fail, results }, 200, cancellation: ct);
    }
}

