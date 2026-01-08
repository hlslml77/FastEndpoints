using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aliyun.Api.LogService;
using Aliyun.Api.LogService.Domain.Log;
using Aliyun.Api.LogService.Infrastructure.Protocol;
using Aliyun.Api.LogService.Infrastructure.Protocol.Http;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Sinks.PeriodicBatching;
using PeriodicBatchingSink = Serilog.Sinks.PeriodicBatching.PeriodicBatchingSink;
using PeriodicBatchingSinkOptions = Serilog.Sinks.PeriodicBatching.PeriodicBatchingSinkOptions;

namespace Web;

/// <summary>
/// Minimal Serilog sink for Aliyun Log Service (SLS) using official SDK: aliyun-log-dotnetcore-sdk.
/// Uses Serilog 4.x native batching (IBatchedLogEventSink).
/// </summary>
public sealed class AliyunSlsBatchedSink : Serilog.Sinks.PeriodicBatching.IBatchedLogEventSink
{
    private readonly ILogServiceClient _client;
    private readonly AliyunSlsOptions _opt;

    public AliyunSlsBatchedSink(AliyunSlsOptions opt)
    {
        ArgumentNullException.ThrowIfNull(opt);
        _opt = opt;

        _client = LogServiceClientBuilders.HttpBuilder
            .Endpoint(opt.Endpoint, opt.Project)
            .Credential(opt.AccessKeyId, opt.AccessKeySecret)
            .Build();
    }

    // NOTE: PeriodicBatching 5.x uses IEnumerable<LogEvent>
    public async Task EmitBatchAsync(IEnumerable<LogEvent> batch)
    {
        if (batch is null)
            return;

        var events = batch as IList<LogEvent> ?? batch.ToList();
        if (events.Count == 0)
            return;

        // Build LogGroupInfo per SDK examples (Benchmark uses PostLogStoreLogsAsync)
        var logGroup = new LogGroupInfo
        {
            Topic = _opt.Topic,
            Source = _opt.Source,
            Logs = events.Select(ToLog).ToList()
        };

        var maxRetries = Math.Max(0, _opt.MaxRetries);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var resp = await _client.PostLogStoreLogsAsync(_opt.LogStore, logGroup);
                resp.EnsureSuccess();
                return; // success
            }
            catch (Exception ex)
            {
                if (attempt >= maxRetries)
                {
                    // give up after retries; write to self log but do NOT throw further
                    SelfLog.WriteLine($"AliyunSLS send failed after {attempt + 1} attempts. err={ex.Message}");
                    return;
                }

                var delayMs = BackoffMs(attempt);
                SelfLog.WriteLine($"AliyunSLS send failed (will retry). attempt={attempt + 1}/{maxRetries + 1} err={ex.Message} delayMs={delayMs}");
                await Task.Delay(delayMs);
            }
        }
    }

    public Task OnEmptyBatchAsync() => Task.CompletedTask;

    

    private static int BackoffMs(int attempt)
    {
        var baseMs = attempt switch { 0 => 200, 1 => 500, 2 => 1000, _ => 2000 };
        return baseMs + Random.Shared.Next(0, 200);
    }

    private static LogInfo ToLog(LogEvent e)
    {
        var contents = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["level"] = e.Level.ToString(),
            ["message"] = e.RenderMessage()
        };

        if (e.Exception != null)
            contents["exception"] = e.Exception.ToString();

        foreach (var kv in e.Properties)
            contents[kv.Key] = kv.Value.ToString();

        return new LogInfo
        {
            // SDK LogInfo.Time is DateTimeOffset (see SDK benchmark)
            Time = e.Timestamp,
            Contents = contents
        };
    }
}

public sealed class AliyunSlsOptions
{
    public string Endpoint { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string AccessKeySecret { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string LogStore { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public string? Source { get; set; } = Environment.MachineName;
    public int MaxRetries { get; set; } = 3;
}

public static class AliyunSlsSerilogExtensions
{
    public static LoggerConfiguration AliyunSls(this LoggerSinkConfiguration cfg, AliyunSlsOptions opt)
    {
        ArgumentNullException.ThrowIfNull(cfg);
        ArgumentNullException.ThrowIfNull(opt);

        // Wrap our batched sink with Serilog's PeriodicBatching sink
        // so Serilog can call it correctly.
        var options = new PeriodicBatchingSinkOptions
        {
            BatchSizeLimit = 100,
            Period = TimeSpan.FromSeconds(2),
            QueueLimit = 10000
        };

        return cfg.Sink(new PeriodicBatchingSink(new AliyunSlsBatchedSink(opt), options));
    }

    public static AliyunSlsOptions? GetAliyunSlsOptions(this IConfiguration config)
        => config.GetSection("AliyunSls").Exists()
            ? config.GetSection("AliyunSls").Get<AliyunSlsOptions>()
            : null;
}

