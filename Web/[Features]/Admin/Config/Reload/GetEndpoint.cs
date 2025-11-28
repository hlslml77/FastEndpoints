using FastEndpoints;
using Serilog;
using Web.Auth;
using Web.Services;

namespace Admin.Config.Reload;

public class GetEndpoint : EndpointWithoutRequest<object>
{
    private readonly IEnumerable<IReloadableConfig> _configs;

    public GetEndpoint(IEnumerable<IReloadableConfig> configs)
    {
        _configs = configs;
    }

    public override void Configure()
    {
        Get("admin/config/reload/{type?}");
        Roles(Role.Admin);
        Description(x => x.WithTags("Admin", "Config").WithSummary("GET 重载配置").WithDescription("按路由或查询参数重载 JSON 配置"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        // 1) 从路由读取
        var type = (Route<string>("type") ?? string.Empty).Trim();
        // 2) 路由为空则从查询读取
        if (string.IsNullOrWhiteSpace(type))
            type = (HttpContext.Request.Query["type"].ToString() ?? string.Empty).Trim();

        type = string.IsNullOrWhiteSpace(type) ? "all" : type.ToLowerInvariant();

        IEnumerable<IReloadableConfig> targets = _configs;
        if (type != "all")
        {
            var set = new HashSet<string>(new[] { "role", "item", "map", "event", "drop" });
            if (!set.Contains(type))
            {
                await HttpContext.Response.SendAsync(new { statusCode = 400, message = $"不支持的类型: {type}" }, 400, cancellation: ct);
                return;
            }
            targets = _configs.Where(c => string.Equals(c.Name, type, StringComparison.OrdinalIgnoreCase));
        }

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

        await HttpContext.Response.SendAsync(new { requested = type, ok, fail, results }, 200, cancellation: ct);
    }
}

