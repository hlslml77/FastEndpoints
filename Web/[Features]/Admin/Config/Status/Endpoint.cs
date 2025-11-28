using FastEndpoints;
using Web.Auth;
using Web.Services;

namespace Admin.Config.Status;

public class Endpoint : EndpointWithoutRequest<object>
{
    private readonly IEnumerable<IReloadableConfig> _configs;

    public Endpoint(IEnumerable<IReloadableConfig> configs)
    {
        _configs = configs;
    }

    public override void Configure()
    {
        Get("admin/config/status");
        Permissions("web_access");
        Description(x => x.WithTags("Admin", "Config").WithSummary("查看配置状态").WithDescription("返回所有可热重载配置的状态信息"));
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        var list = _configs.Select(c => c.GetStatus()).ToList();
        return HttpContext.Response.SendAsync(list, 200, cancellation: ct);
    }
}

