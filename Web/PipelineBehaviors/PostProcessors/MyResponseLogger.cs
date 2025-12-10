using Serilog;
using Web.Services;
using System.Diagnostics;

namespace Web.PipelineBehaviors.PostProcessors;

public class MyResponseLogger<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    private const string StartKey = "__req_start_ticks";

    public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> context, CancellationToken ct)
    {
        var http = context.HttpContext;
        var method = http.Request.Method;
        var path = http.Request.Path.Value;
        var traceId = http.TraceIdentifier;
        var status = http.Response?.StatusCode;
        var contentType = http.Response?.ContentType ?? string.Empty;

        double? elapsedMs = null;
        if (http.Items.TryGetValue(StartKey, out var startObj) && startObj is long startTicks)
        {
            elapsedMs = Stopwatch.GetElapsedTime(startTicks).TotalMilliseconds;
        }

        string output = "<no-body>";
        if (!contentType.Contains("octet-stream", StringComparison.OrdinalIgnoreCase) &&
            !contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            output = LogSanitizer.ToSafeJson(context.Response);
        }

        if (elapsedMs.HasValue)
            Log.Information("RES {Method} {Path} status={Status} elapsed={ElapsedMs}ms output={Output} trace={TraceId}", method, path, status, Math.Round(elapsedMs.Value, 2), output, traceId);
        else
            Log.Information("RES {Method} {Path} status={Status} output={Output} trace={TraceId}", method, path, status, output, traceId);

        return Task.CompletedTask;
    }
}