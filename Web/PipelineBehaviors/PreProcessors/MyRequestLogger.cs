using Serilog;
using Web.Services;
using System.Diagnostics;

namespace Web.PipelineBehaviors.PreProcessors;

public class MyRequestLogger<TRequest> : IPreProcessor<TRequest>
    where TRequest : notnull
{
    private const string StartKey = "__req_start_ticks";

    public Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        var http = context.HttpContext;
        http.Items[StartKey] = Stopwatch.GetTimestamp();

        var method = http.Request.Method;
        var path = http.Request.Path.Value;
        var query = http.Request.QueryString.HasValue ? http.Request.QueryString.Value : string.Empty;
        var traceId = http.TraceIdentifier;

        var input = LogSanitizer.ToSafeJson(context.Request);

        Log.Information("REQ {Method} {Path} q={Query} input={Input} trace={TraceId}", method, path, query, input, traceId);
        return Task.CompletedTask;
    }
}
