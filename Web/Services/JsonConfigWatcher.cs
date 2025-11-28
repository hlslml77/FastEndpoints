using System;
using System.IO;
using System.Threading;
using Serilog;

namespace Web.Services;

public sealed class JsonConfigWatcher : IDisposable
{
    private readonly FileSystemWatcher _fsw;
    private readonly Timer _debounce;
    private readonly TimeSpan _delay;
    private readonly Action _onChange;
    private int _pending;

    public JsonConfigWatcher(string directory, string filter, Action onChange, TimeSpan? debounce = null)
    {
        if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        _onChange = onChange ?? throw new ArgumentNullException(nameof(onChange));
        _delay = debounce ?? TimeSpan.FromMilliseconds(300);
        _debounce = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref _pending, 0) > 0)
            {
                try { _onChange(); }
                catch (Exception ex) { Log.Error(ex, "Config reload failed for {Dir}/{Filter}", directory, filter); }
            }
        });

        _fsw = new FileSystemWatcher(directory, filter)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };

        _fsw.Changed += OnFsEvent;
        _fsw.Created += OnFsEvent;
        _fsw.Renamed += OnFsEvent;
        _fsw.Deleted += OnFsEvent;
    }

    private void OnFsEvent(object? sender, FileSystemEventArgs e)
    {
        Interlocked.Increment(ref _pending);
        _debounce.Change(_delay, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _fsw.Dispose();
        _debounce.Dispose();
        GC.SuppressFinalize(this);
    }
}

