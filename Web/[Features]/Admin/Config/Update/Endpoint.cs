using System.Text.Encodings.Web;
using System.Text.Json;
using FastEndpoints;
using Serilog;
using Web.Auth;
using Web.Services;

namespace Admin.Config.Update;

public sealed class FileUpdate
{
    public string? File { get; set; }
    public JsonElement Content { get; set; }
}

public sealed class Request
{
    public List<FileUpdate>? Files { get; set; }
}

public sealed class UpdateResult
{
    public string File { get; set; } = string.Empty;
    public string Status { get; set; } = "ok"; // ok | error
    public string? Error { get; set; }
    public string? Backup { get; set; }
    public long? BytesWritten { get; set; }
}

public sealed class Response
{
    public int Ok { get; set; }
    public int Fail { get; set; }
    public List<UpdateResult> Results { get; set; } = new();
}

public class Endpoint : Endpoint<Request, Response>
{
    private readonly IEnumerable<IReloadableConfig> _configs;
    public Endpoint(IEnumerable<IReloadableConfig> configs)
    {
        _configs = configs;
    }

    public override void Configure()
    {
        Post("admin/config/update");
        Description(x => x
            .WithTags("Admin", "Config")
            .WithSummary("批量更新 JSON 配表")
            .WithDescription("允许管理员一次性更新 Web/Json 下的一个或多个 .json 文件（原子替换、自动备份）。"));
        Options(o => o.Accepts<Request>("application/json"));
    }

    private static readonly JsonSerializerOptions s_writeOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var res = new Response();

        if (req.Files is null || req.Files.Count == 0)
        {
            await HttpContext.Response.SendAsync(new Response
            {
                Ok = 0,
                Fail = 1,
                Results = new() { new UpdateResult { File = string.Empty, Status = "error", Error = "files is required" } }
            }, 400, cancellation: ct);
            return;
        }

        // base Json directory used by services
        var baseDir = Path.Combine(AppContext.BaseDirectory, "Json");
        Directory.CreateDirectory(baseDir);
        var baseFull = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        var updatedFiles = new List<string>();
        foreach (var f in req.Files)
        {
            var r = new UpdateResult { File = f.File ?? string.Empty };

            try
            {
                if (string.IsNullOrWhiteSpace(f.File))
                    throw new ArgumentException("file is empty");

                var fileName = Path.GetFileName(f.File.Trim());
                if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("only .json files are allowed");

                var target = Path.Combine(baseDir, fileName);
                var targetFull = Path.GetFullPath(target);
                if (!targetFull.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                    throw new UnauthorizedAccessException("path traversal detected");

                if (!File.Exists(target))
                    throw new FileNotFoundException("target json not found", fileName);

                // minimal shape guard: most configs expect top-level array
                if (f.Content.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("invalid content: root must be a JSON array");

                // write to temp file first
                var temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
                long bytesWritten = 0;

                var json = JsonSerializer.Serialize(f.Content, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                await File.WriteAllBytesAsync(temp, bytes, ct);
                bytesWritten = bytes.LongLength;

                // backup existing
                string? backupPath = null;
                if (File.Exists(target))
                {
                    var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    backupPath = target + "." + ts + ".bak";
                    File.Copy(target, backupPath, overwrite: false);
                }

                // atomic replace
                File.Move(temp, target, overwrite: true);
                // ensure fsw change event by touching last write time
                try { File.SetLastWriteTimeUtc(target, DateTime.UtcNow); } catch { /* ignore */ }

                r.Status = "ok";
                r.Backup = backupPath is not null ? Path.GetFileName(backupPath) : null;
                r.BytesWritten = bytesWritten;
                res.Ok++;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Config update failed for {File}", f.File);
                r.Status = "error";
                r.Error = ex.Message;
                res.Fail++;
            }

            res.Results.Add(r);
        }

        // Force immediate reload to ensure new configs are read right away
        foreach (var c in _configs)
        {
            try { c.Reload(); }
            catch (Exception ex) { Log.Error(ex, "Post-update reload failed for {Name}", c.Name); }
        }

        await HttpContext.Response.SendAsync(res, 200, cancellation: ct);
    }
}

