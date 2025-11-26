using Serilog;

namespace Web.PipelineBehaviors.PreProcessors;

public class MyRequestLogger<TRequest> : IPreProcessor<TRequest>
    where TRequest : notnull
{
    public Task PreProcessAsync(IPreProcessorContext<TRequest> context, CancellationToken ct)
    {
        Log.Information("request:{RequestType} path:{Path}", context.Request?.GetType().FullName, context.HttpContext.Request.Path);
        return Task.CompletedTask;
    }
}
