using Serilog;

namespace Web.PipelineBehaviors.PostProcessors;

public class MyResponseLogger<TRequest, TResponse> : IPostProcessor<TRequest, TResponse>
    where TRequest : notnull
{
    public Task PostProcessAsync(IPostProcessorContext<TRequest, TResponse> context, CancellationToken ct)
    {
        if (context.Response is Sales.Orders.Create.Response response)
        {
            Log.Warning("sale complete: {OrderId}", response?.OrderID);
        }

        return Task.CompletedTask;
    }
}