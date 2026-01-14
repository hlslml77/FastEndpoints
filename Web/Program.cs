using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using FastEndpoints.Swagger;
using NJsonSchema;
using NSwag;
using Serilog;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.CommandHandlerTest;
using TestCases.EventHandlingTest;
using TestCases.EventQueueTest;
using TestCases.GlobalGenericProcessorTest;
using TestCases.JobQueueTest;
using TestCases.KeyedServicesTests;
using TestCases.ProcessorStateTest;
using TestCases.ServerStreamingTest;
using TestCases.UnitTestConcurrencyTest;
using Web;
using Web.PipelineBehaviors.PreProcessors;
using Web.PipelineBehaviors.PostProcessors;
using Web.Services;


// 禁用 wwwroot 目录（纯 API 项目不需要静态文件）
var bld = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = string.Empty
});

// Kestrel/HTTP keep-alive tuning to improve connection reuse and reduce handshake churn
bld.WebHost.ConfigureKestrel(o =>
{
    // keep idle connections around longer so clients can reuse TCP/TLS sessions
    o.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);

    // allow headers/body to arrive without the server tearing down the connection too aggressively
    o.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);

    // protect against large headers while keeping defaults reasonable
    // (raise only if you actually need bigger headers)
    o.Limits.MaxRequestHeadersTotalSize = 64 * 1024;

    // for slow client uploads; adjust if you have large uploads
    o.Limits.MinRequestBodyDataRate = null;
    o.Limits.MinResponseDataRate = null;

    // enable both HTTP/1.1 (keep-alive) and HTTP/2 multiplexing
    o.ConfigureEndpointDefaults(e =>
    {
        e.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
});
#pragma warning disable CS0618 // suppress obsolete MySqlConnectorLogManager.Provider warning
// Enable MySqlConnector internal logging if env var MYSQL_DEBUG=1
if (Environment.GetEnvironmentVariable("MYSQL_DEBUG") == "1")
{
    MySqlConnector.Logging.MySqlConnectorLogManager.Provider = new MySqlConnector.Logging.ConsoleLoggerProvider(MySqlConnector.Logging.MySqlConnectorLogLevel.Info);
    Console.WriteLine("--> MySqlConnector internal logging ENABLED (set MYSQL_DEBUG=0 to disable)");
}
// 1. Enable Serilog Self-Logging for troubleshooting the sink
Serilog.Debugging.SelfLog.Enable(Console.Error);

// configure Serilog (console + file + Aliyun SLS)
bld.Logging.ClearProviders();
var serilogConfig = new LoggerConfiguration()
    .ReadFrom.Configuration(bld.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/web-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7);

// optional: push logs to Aliyun SLS (aliyun-log-dotnetcore-sdk)
var aliyunSlsOptions = bld.Configuration.GetAliyunSlsOptions();
if (aliyunSlsOptions is not null
    && !string.IsNullOrWhiteSpace(aliyunSlsOptions.Endpoint)
    && !string.IsNullOrWhiteSpace(aliyunSlsOptions.AccessKeyId)
    && !string.IsNullOrWhiteSpace(aliyunSlsOptions.AccessKeySecret)
    && !string.IsNullOrWhiteSpace(aliyunSlsOptions.Project)
    && !string.IsNullOrWhiteSpace(aliyunSlsOptions.LogStore))
{
    serilogConfig.WriteTo.AliyunSls(aliyunSlsOptions);
    Console.WriteLine("--> Aliyun SLS sink configured.");
}
else
{
    Console.WriteLine("--> Aliyun SLS sink NOT configured (missing settings).");
}

Log.Logger = serilogConfig.CreateLogger();

// 2. Add a test log message to verify the sink is working
Log.Warning("SLS TEST: This is a test warning message sent at {Timestamp}", DateTime.UtcNow);

bld.Host.UseSerilog(Log.Logger, dispose: true);


bld.AddHandlerServer();

// 初始化数据库（使用 Serilog）
var dbInitializer = new DatabaseInitializationService(bld.Configuration);
await dbInitializer.InitializeAsync();

bld.Services.AddDbContext<Web.Data.AppDbContext>(o =>
    {
        var cs = bld.Configuration.GetConnectionString("DefaultConnection");
        o.UseMySql(cs, ServerVersion.AutoDetect(cs));
        // keep database logs concise and safe
        o.EnableSensitiveDataLogging(false);
        o.EnableDetailedErrors(false);
        // register interceptor to log only slow queries (>200ms)
        o.AddInterceptors(new SlowQueryInterceptor(200));
    });

// used by background jobs / fire-and-forget work to safely create a new dbcontext instance
// NOTE: use Scoped lifetime to avoid resolving scoped DbContextOptions from a singleton factory
bld.Services.AddDbContextFactory<Web.Data.AppDbContext>(
    (sp, o) =>
    {
        var cs = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
        o.UseMySql(cs, ServerVersion.AutoDetect(cs));
        o.EnableSensitiveDataLogging(false);
        o.EnableDetailedErrors(false);
        o.AddInterceptors(new SlowQueryInterceptor(200));
    },
    ServiceLifetime.Scoped);
bld.Services
   .AddCors()
   .AddOutputCache()
   .AddIdempotency()
   .AddResponseCaching()
   .AddFastEndpoints(o => o.SourceGeneratorDiscoveredTypes = DiscoveredTypes.All)
   .AddAuthenticationJwtBearer(s => s.SigningKey = bld.Configuration["TokenKey"]!)
   .AddAuthorization(o => o.AddPolicy("AdminOnly", b => b.RequireRole(Role.Admin)))
   .AddKeyedTransient<IKeyedService>("AAA", (_, _) => new MyKeyedService("AAA"))
   .AddKeyedTransient<IKeyedService>("BBB", (_, _) => new MyKeyedService("BBB"))
   .AddScoped<IEmailService, EmailService>();

// 添加HttpClient用于调用APP服务进行token验证
// NOTE: BaseUrl must be configured (AppService:BaseUrl). If missing, fall back to a safe default
// to avoid startup crash in dev environments.
bld.Services.AddHttpClient("AppService", client =>
{
    var baseUrl = bld.Configuration["AppService:BaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        // keep service alive even if the external app service isn't configured
        // (endpoints that depend on this client may still fail when called)
        baseUrl = "http://127.0.0.1:0";
        Log.Warning("Missing config AppService:BaseUrl. Using placeholder BaseAddress: {BaseUrl}", baseUrl);
    }

    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// internal self api client for endpoints to call same service (e.g., DingTalkEventsEndpoint)
bld.Services.AddHttpClient("SelfApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
// add redis cache if available; else fall back to memory-only
static bool CanConnectRedis(string? cs)
{
    if (string.IsNullOrWhiteSpace(cs)) return false;
    try
    {
        var hostPart = cs.Split(',')[0];
        var host = hostPart;
        var port = 6379;
        if (hostPart.Contains(":"))
        {
            var hp = hostPart.Split(':');
            host = hp[0];
            if (hp.Length > 1 && int.TryParse(hp[1], out var p)) port = p;
        }
        using var client = new System.Net.Sockets.TcpClient();
        var task = client.ConnectAsync(host, port);
        return task.Wait(TimeSpan.FromMilliseconds(500));
    }
    catch { return false; }
}
var redisCs = bld.Configuration.GetConnectionString("Redis");
if (CanConnectRedis(redisCs))
{
    bld.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisCs;
        options.InstanceName = "Web_";
    });
    Log.Information("Redis detected at {RedisCs}. Using distributed cache.", redisCs);
}
else
{
    Log.Warning("Redis not detected. Falling back to memory cache only.");
}

bld.Services
   // Register concrete singletons once, then map to multiple interfaces to share the same instance
   .AddSingleton<RoleConfigService>()
   .AddSingleton<IRoleConfigService>(sp => sp.GetRequiredService<RoleConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<RoleConfigService>())

   .AddSingleton<MapConfigService>()
   .AddSingleton<IMapConfigService>(sp => sp.GetRequiredService<MapConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<MapConfigService>())

   .AddSingleton<ItemConfigService>()
   .AddSingleton<IItemConfigService>(sp => sp.GetRequiredService<ItemConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<ItemConfigService>())

   .AddSingleton<CollectionConfigService>()
   .AddSingleton<ICollectionConfigService>(sp => sp.GetRequiredService<CollectionConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<CollectionConfigService>())

   .AddSingleton<TravelEventConfigService>()
   .AddSingleton<ITravelEventConfigService>(sp => sp.GetRequiredService<TravelEventConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<TravelEventConfigService>())

   .AddSingleton<TravelDropPointConfigService>()
   .AddSingleton<ITravelDropPointConfigService>(sp => sp.GetRequiredService<TravelDropPointConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<TravelDropPointConfigService>())

   // Monster config
   .AddSingleton<MonsterConfigService>()
   .AddSingleton<IMonsterConfigService>(sp => sp.GetRequiredService<MonsterConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<MonsterConfigService>())

   // Random world event config
   .AddSingleton<RandomWorldEventConfigService>()
   .AddSingleton<IRandomWorldEventConfigService>(sp => sp.GetRequiredService<RandomWorldEventConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<RandomWorldEventConfigService>())

   // General config (Config.json/config.json)
   .AddSingleton<GeneralConfigService>()
   .AddSingleton<IGeneralConfigService>(sp => sp.GetRequiredService<GeneralConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<GeneralConfigService>())

   // PVE Rank
   .AddSingleton<PveRankConfigService>()
   .AddSingleton<IPveRankConfigService>(sp => sp.GetRequiredService<PveRankConfigService>())
   .AddSingleton<IReloadableConfig>(sp => sp.GetRequiredService<PveRankConfigService>())

   .AddScoped<IPlayerRoleService, PlayerRoleService>()
   .AddScoped<IMapService, MapService>()
   .AddScoped<IInventoryService, InventoryService>()
   .AddScoped<ICollectionService, CollectionService>()
   .AddScoped<IGameStatisticsService, GameStatisticsService>()
   .AddScoped<IPveRankService, PveRankService>()
   .AddHostedService<GameStatisticsBackgroundJob>()
   .AddSingleton(new SingltonSVC(0))
   .AddJobQueues<Job, JobStorage>()
   .RegisterServicesFromWeb()
   .AddAntiforgery()
   .AddMemoryCache()
   .SwaggerDocument(
       o =>
       {
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
           o.DocumentSettings =
               s =>
               {
                   s.DocumentName = "Initial Release";
                   s.Title = "Web API";
                   s.Version = "v0.0";
                   s.SchemaSettings.SchemaType = SchemaType.OpenApi3;
               };
           o.TagCase = TagCase.TitleCase;
           o.TagStripSymbols = true;
           o.RemoveEmptyRequestSchema = false;
       })
   .SwaggerDocument(
       o =>
       {
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
           o.DocumentSettings =
               s =>
               {
                   s.DocumentName = "Release 1.0";
                   s.Title = "Web API";
                   s.Version = "v1.0";
                   s.AddAuth(
                       "ApiKey",
                       new()
                       {
                           Name = "api_key",
                           In = OpenApiSecurityApiKeyLocation.Header,
                           Type = OpenApiSecuritySchemeType.ApiKey
                       });
               };
           o.MaxEndpointVersion = 1;
           o.RemoveEmptyRequestSchema = false;
           o.TagStripSymbols = true;
       })
   .SwaggerDocument(
       o =>
       {
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
           o.DocumentSettings =
               s =>
               {
                   s.DocumentName = "Release 2.0";
                   s.Title = "FastEndpoints Sandbox";
                   s.Version = "v2.0";
               };
           o.MaxEndpointVersion = 2;
           o.ShowDeprecatedOps = true;
           o.RemoveEmptyRequestSchema = false;
           o.TagStripSymbols = true;
       })
   .SwaggerDocument(
       o => //only ver3 & only FastEndpoints
       {
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is not true;
           o.DocumentSettings =
               s =>
               {
                   s.DocumentName = "Release 3.0";
                   s.Title = "FastEndpoints Sandbox ver3 only";
                   s.Version = "v3.0";
               };
           o.MinEndpointVersion = 3;
           o.MaxEndpointVersion = 3;
           o.ExcludeNonFastEndpoints = true;
       })

   //used for release versioning tests
   .SwaggerDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is true;
           o.DocumentSettings = d =>
                                {
                                    d.Title = "Web API";
                                    d.DocumentName = "ReleaseVersioning - v0";
                                };
           o.ReleaseVersion = 0;
           o.ShowDeprecatedOps = true;
       })
   .SwaggerDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is true;
           o.DocumentSettings = d =>
                                {
                                    d.Title = "Web API";
                                    d.DocumentName = "ReleaseVersioning - v1";
                                };
           o.ReleaseVersion = 1;
           o.ShowDeprecatedOps = true;
       })
   .SwaggerDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is true;
           o.DocumentSettings = d =>
                                {
                                    d.Title = "Web API";
                                    d.DocumentName = "ReleaseVersioning - v2";
                                };
           o.ReleaseVersion = 2;
           o.ShowDeprecatedOps = true;
       })
   .SwaggerDocument(
       o =>
       {
           o.ExcludeNonFastEndpoints = true;
           o.EndpointFilter = ep => ep.EndpointTags?.Contains("release_versioning") is true;
           o.DocumentSettings = d =>
                                {
                                    d.Title = "Web API";
                                    d.DocumentName = "ReleaseVersioning - v3";
                                };
           o.ReleaseVersion = 3;
           o.ShowDeprecatedOps = true;
       });

var supportedCultures = new[] { new CultureInfo("en-US") };

var app = bld.Build();
app.UseRequestLocalization(
       new RequestLocalizationOptions
       {
           DefaultRequestCulture = new("en-US"),
           SupportedCultures = supportedCultures,
           SupportedUICultures = supportedCultures
       })
   .UseDefaultExceptionHandler()
   .UseResponseCaching()
   .UseRouting() //if using, this call must go before auth/cors/fastendpoints middleware
   .UseCors(b => b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod())
   .UseJwtRevocation<JwtBlacklistChecker>()
   .UseAuthentication()
   .UseAuthorization()
   .UseAntiforgeryFE(additionalContentTypes: new[] { "application/json" })
   .UseOutputCache()
   .UseFastEndpoints(
       c =>
       {
           c.Validation.EnableDataAnnotationsSupport = true;

           c.Binding.UsePropertyNamingPolicy = true;
           c.Binding.ReflectionCache.AddFromWeb();
           c.Binding.ValueParserFor<Guid>(x => new(Guid.TryParse(x, out var res), res));

           c.Endpoints.RoutePrefix = "api";
           c.Endpoints.ShortNames = false;
           // 禁用按首个 Tag 自动前缀，避免路由多出 "mapsystem/"
           c.Endpoints.PrefixNameWithFirstTag = false;
           c.Endpoints.Filter = ep => ep.EndpointTags?.Contains("exclude") is not true;
           c.Endpoints.Configurator =
               ep =>
               {
                   ep.PreProcessors(Order.Before, typeof(GlobalGenericPreProcessor<>));
                   ep.PostProcessors(Order.After, typeof(GlobalGenericPostProcessor<,>));
                   ep.PreProcessor<GlobalStatePreProcessor>(Order.Before);
                   ep.PreProcessors(Order.Before, new AdminHeaderChecker());

                   // log request/response for every endpoint
                   ep.PreProcessors(Order.Before, typeof(Web.PipelineBehaviors.PreProcessors.MyRequestLogger<>));
                   ep.PostProcessors(Order.After, typeof(Web.PipelineBehaviors.PostProcessors.MyResponseLogger<,>));

                   if (ep.EndpointTags?.Contains("Orders") is true)
                       ep.Description(b => b.Produces<ErrorResponse>(400, "application/problem+json"));
               };

           c.Versioning.Prefix = "ver";

           c.Throttle.HeaderName = "X-Custom-Throttle-Header";
           c.Throttle.Message = "Custom Error Response";
       })
   .UseEndpoints(
       c => //this must go after usefastendpoints (only if using endpoints)
       {
           c.MapGet("test", () => "hello world!").WithTags("map-get");
           c.MapGet("test/{testId:int?}", (int? testId) => $"hello {testId}").WithTags("map-get");
       });
app.MapGet("api/admin/config/reload2/{type?}", (HttpContext ctx, string? type) =>
{
    var configs = ctx.RequestServices.GetRequiredService<IEnumerable<Web.Services.IReloadableConfig>>();
    if (!(ctx.User?.IsInRole("admin") ?? false))
        return Results.Json(new { statusCode = 403, message = "forbidden" }, statusCode: 403);

    var t = (type ?? ctx.Request.Query["type"].ToString() ?? "").Trim();
    t = string.IsNullOrWhiteSpace(t) ? "all" : t.ToLowerInvariant();
    IEnumerable<Web.Services.IReloadableConfig> targets = configs;
    if (t != "all")
    {
        var set = new HashSet<string>(new[] { "role", "item", "map", "event", "drop", "monster" });
        if (!set.Contains(t))
            return Results.Json(new { statusCode = 400, message = $"不支持的类型: {t}" }, statusCode: 400);
        targets = configs.Where(c => string.Equals(c.Name, t, StringComparison.OrdinalIgnoreCase));
    }

    int ok = 0, fail = 0;
    var results = new List<object>();
    foreach (var c2 in targets)
    {
        try { c2.Reload(); ok++; results.Add(new { name = c2.Name, status = "ok", c2.LastReloadTime }); }
        catch (Exception ex) { fail++; results.Add(new { name = c2.Name, status = "error", error = ex.Message }); }
    }

    return Results.Json(new { requested = t, ok, fail, results });
}).WithTags("Admin", "Config");


if (!app.Environment.IsProduction())
    app.UseSwaggerGen();

app.Services.RegisterGenericCommand(typeof(GenericCommand<>), typeof(GenericCommandHandler<>));
app.Services.RegisterGenericCommand(typeof(GenericNoResultCommand<>), typeof(GenericNoResultCommandHandler<>));
app.Services.RegisterGenericCommand<JobTestGenericCommand<SomeEvent>, JobTestGenericCommandHandler<SomeEvent>>();

app.MapHandlers(
    h =>
    {
        h.Register<VoidCommand, VoidCommandHandler>();
        h.Register<SomeCommand, SomeCommandHandler, string>();
        h.Register<EchoCommand, EchoCommandHandler, EchoCommand>();
        h.RegisterServerStream<StatusStreamCommand, StatusUpdateHandler, StatusUpdate>();
        h.RegisterClientStream<CurrentPosition, PositionProgressHandler, ProgressReport>();
        h.RegisterEventHub<TestEventQueue>();
        h.RegisterEventHub<MyEvent>();
    });

app.UseJobQueues(
    o =>
    {
        o.MaxConcurrency = 4;
        o.LimitsFor<JobTestCommand>(1, TimeSpan.FromSeconds(1));
        o.LimitsFor<JobCancelTestCommand>(100, TimeSpan.FromSeconds(60));
        o.StorageProbeDelay = TimeSpan.FromMilliseconds(100);
    });

var isTestHost = app.Services.CreateScope().ServiceProvider.GetService<IEmailService>() is not EmailService;

if (isTestHost && app.Environment.EnvironmentName != "Testing")
    throw new InvalidOperationException("TestFixture hasn't set the test environment correctly!");

app.Run();