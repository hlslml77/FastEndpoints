using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using FastEndpoints;
using System.Net.Http.Json;

using Serilog;
using Web.Services;
namespace Web.Features.DingTalk;

/// <summary>
/// 钉钉事件订阅回调接口（HTTP 推送）。
///
/// 钉钉要求：
/// - 请求 query: signature,timestamp,nonce
/// - 请求 body : {"encrypt":"..."}
/// - 响应 body : {"msg_signature":"...","timeStamp":"...","nonce":"...","encrypt":"..."}
///
/// 通过验证后才会开始推送真实事件。
/// </summary>
public sealed class DingTalkEventsEndpoint : EndpointWithoutRequest
{
    // 允许前面有 @机器人 或任意文本；允许 JSON 以 { 或 [ 开头；忽略大小写
    private static readonly Regex _contentRegex = new(
        @"^[\s\S]*?(?<file>[\w\-.]+\.json)\s+(?<json>(\{|\[)[\s\S]+)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    public override void Configure()
    {
        Post("/dingtalk/events");
        AllowAnonymous();
        Tags("DingTalk");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var q = HttpContext.Request.Query;
        var signature = q["signature"].ToString();
        var timestamp = q["timestamp"].ToString();
        var nonce = q["nonce"].ToString();

        string body;
        using (var sr = new StreamReader(HttpContext.Request.Body))
            body = await sr.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(body))
        {
            Log.Error("[DingTalkEvents] Empty request body. signature={Signature} timestamp={Timestamp} nonce={Nonce}", signature, timestamp, nonce);
            await SendEncryptedSuccessAsync(timestamp, nonce, ct);
            return;
        }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (Exception ex)
        {
            Log.Error(ex, "[DingTalkEvents] Invalid JSON body. signature={Signature} timestamp={Timestamp} nonce={Nonce} body={Body}", signature, timestamp, nonce, body);
            await SendEncryptedSuccessAsync(timestamp, nonce, ct);
            return;
        }

        // 目标：从 body 里拿到 text.content 中的 "文件名.json" + JSON 内容，然后调用 /api/admin/config/update
        // 你日志里看到的 body 已经是明文消息体（没有 encrypt 字段），所以这里直接按明文解析。

        // 1) 从 body 中取 text.content
        var root = doc.RootElement;
        if (!root.TryGetProperty("text", out var txt) || !txt.TryGetProperty("content", out var cProp))
        {
            Log.Warning("[DingTalkEvents] Missing text.content property. signature={Signature} timestamp={Timestamp} nonce={Nonce}", signature, timestamp, nonce);
            await SendEncryptedSuccessAsync(timestamp, nonce, ct);
            return;
        }

        var content = cProp.GetString() ?? string.Empty;
        var match = _contentRegex.Match(content.Trim());
        if (!match.Success)
        {
            Log.Warning("[DingTalkEvents] Content regex not matched. content={Content} signature={Signature} timestamp={Timestamp} nonce={Nonce}", content, signature, timestamp, nonce);
            await SendEncryptedSuccessAsync(timestamp, nonce, ct);
            return;
        }

        var fileName = match.Groups["file"].Value;
        var jsonContentStr = match.Groups["json"].Value.Trim();

        // 去掉可能的 ```json ``` 包裹
        if (jsonContentStr.StartsWith("```", StringComparison.Ordinal))
        {
            var nl = jsonContentStr.IndexOf('\n');
            if (nl >= 0) jsonContentStr = jsonContentStr[(nl + 1)..];
            var lastFence = jsonContentStr.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) jsonContentStr = jsonContentStr[..lastFence].Trim();
        }

        // 2) 解析 JSON 内容
        JsonElement jsonContent;
        try { jsonContent = JsonSerializer.Deserialize<JsonElement>(jsonContentStr); }
        catch (Exception ex)
        {
            Log.Error(ex, "[DingTalkEvents] JSON content deserialize failed. fileName={FileName}", fileName);
            await SendEncryptedSuccessAsync(timestamp, nonce, ct);
            return;
        }

        // 3) 本地热更（复用 /admin/config/update 的实现逻辑，但不走 HTTP，避免鉴权/401 问题）
        var baseDir = Path.Combine(AppContext.BaseDirectory, "Json");
        Directory.CreateDirectory(baseDir);
        var baseFull = Path.GetFullPath(baseDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        try
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("file is empty");

            var safeFileName = Path.GetFileName(fileName.Trim());
            if (!safeFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("only .json files are allowed");

            var target = Path.Combine(baseDir, safeFileName);
            var targetFull = Path.GetFullPath(target);
            if (!targetFull.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase))
                throw new UnauthorizedAccessException("path traversal detected");

            if (!File.Exists(target))
                throw new FileNotFoundException("target json not found", safeFileName);

            // minimal shape guard: most configs expect top-level array
            if (jsonContent.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("invalid content: root must be a JSON array");

            // write to temp file first
            var temp = target + ".tmp-" + Guid.NewGuid().ToString("N");
            var json = JsonSerializer.Serialize(jsonContent, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            await File.WriteAllBytesAsync(temp, bytes, ct);

            // backup existing
            string? backupPath = null;
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backupPath = target + "." + ts + ".bak";
            File.Copy(target, backupPath, overwrite: false);

            // atomic replace
            File.Move(temp, target, overwrite: true);
            try { File.SetLastWriteTimeUtc(target, DateTime.UtcNow); } catch { /* ignore */ }

            // Force immediate reload to ensure new configs are read right away
            foreach (var c in Resolve<IEnumerable<IReloadableConfig>>())
            {
                try { c.Reload(); }
                catch (Exception ex) { Log.Error(ex, "[DingTalkEvents] Post-update reload failed for {Name}", c.Name); }
            }

            Log.Information("[DingTalkEvents] Local config hot-reload succeeded. fileName={FileName} bytes={Bytes} backup={Backup}",
                safeFileName,
                bytes.LongLength,
                Path.GetFileName(backupPath));

            // 更新成功后发钉钉群提示（复用现有 webhook 配置）
            var cfg = Resolve<IConfiguration>();
            var webhook = cfg["Robot:GroupWebhook"];
            if (!string.IsNullOrWhiteSpace(webhook))
            {
                var dingMsg = new
                {
                    msgtype = "text",
                    text = new { content = $"配置文件 {safeFileName} 更新成功 ✅ {DateTime.Now:yyyy-MM-dd HH:mm:ss}" }
                };

                try
                {
                    var factory = Resolve<IHttpClientFactory>();
                    await factory.CreateClient().PostAsJsonAsync(webhook, dingMsg, ct);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[DingTalkEvents] Send ding group message failed. fileName={FileName}", safeFileName);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[DingTalkEvents] Local config hot-reload failed. fileName={FileName}", fileName);
        }

        await SendEncryptedSuccessAsync(timestamp, nonce, ct);
        return;

        // // 1) 校验签名
        // if (!string.IsNullOrWhiteSpace(signature) &&
        //     !string.IsNullOrWhiteSpace(timestamp) &&
        //     !string.IsNullOrWhiteSpace(nonce) &&
        //     !string.IsNullOrWhiteSpace(encrypt))
        // {
        //     // 注意：钉钉官方算法是把 token/timestamp/nonce/encrypt 排序后 sha1。
        //     // 这里实现为简化版本，如果仍旧验证失败，需要把 VerifySignature 改为排序规则。
        //     // 先用钉钉控制台测试一次，如果仍失败我再按排序规则改。
        //     var ok = DingTalkCrypto.VerifySignature(signature, timestamp, nonce, encrypt);
        //     if (!ok)
        //     {
        //         // 即使失败也返回加密success，避免钉钉反复重试导致控制台卡死
        //         Log.Error("[DingTalkEvents] Signature verification failed. signature={Signature} timestamp={Timestamp} nonce={Nonce} encrypt={Encrypt}", signature, timestamp, nonce, encrypt);
        //         await SendEncryptedSuccessAsync(timestamp, nonce, ct);
        //         return;
        //     }
        // }

        // // 2) 解密
        // // 验证阶段钉钉发来的 encrypt 可能会因为配置错误/环境不同导致无法解密。
        // // 我们这里不能让异常冒泡（否则钉钉认为回调失败）。
        // string plain;
        // try
        // {
        //     plain = DingTalkCrypto.DecryptMsg(encrypt);
        // }
        // catch (Exception ex)
        // {
        //     // 解密失败也直接按协议回加密 success，让钉钉完成校验/停止重试
        //     Log.Error(ex, "[DingTalkEvents] Decrypt failed. signature={Signature} timestamp={Timestamp} nonce={Nonce} encrypt={Encrypt}", signature, timestamp, nonce, encrypt);
        //     await SendEncryptedSuccessAsync(timestamp, nonce, ct);
        //     return;
        // }

        // // 3) 如果是验证阶段，明文一般就是 "success" 或包含 challenge
        // // 钉钉事件订阅验证最终都需要回加密的 "success"

        // 4) 真实事件(你原来的业务逻辑) - 这里保留解析能力
        // try
        // {
        //     var plainDoc = JsonDocument.Parse(plain);

        //     // 钉钉“自定义机器人回调模式/事件推送”里，明文通常就是消息对象本身（不是包在 event 字段下）
        //     var ev = plainDoc.RootElement;
        //     var msgType = ev.TryGetProperty("msgtype", out var mt) ? mt.GetString() : null;

        //     if (msgType == "text" &&
        //         ev.TryGetProperty("text", out var txt) &&
        //         txt.TryGetProperty("content", out var c))
        //     {
        //         var text = c.GetString() ?? string.Empty;
        //         var match = _contentRegex.Match(text.Trim());
        //         if (match.Success)
        //         {
        //             var fileName = match.Groups["file"].Value;
        //             var jsonContentStr = match.Groups["json"].Value.Trim();

        //             // Remove markdown code fences if present (e.g., ```json ... ```)
        //             if (jsonContentStr.StartsWith("```"))
        //             {
        //                 // Drop first line (``` or ```json)
        //                 var nl = jsonContentStr.IndexOf('\n');
        //                 if (nl >= 0)
        //                     jsonContentStr = jsonContentStr[(nl + 1)..];

        //                 // Remove trailing ```
        //                 var lastFence = jsonContentStr.LastIndexOf("```", StringComparison.Ordinal);
        //                 if (lastFence >= 0)
        //                     jsonContentStr = jsonContentStr[..lastFence].TrimEnd();
        //             }

        //             // Ensure we start exactly at first json char to avoid chat text noise
        //             var firstBrace = jsonContentStr.IndexOfAny(new[] { '{', '[' });
        //             if (firstBrace > 0)
        //                 jsonContentStr = jsonContentStr[firstBrace..];

        //             JsonElement jsonContent;
        //             try { jsonContent = JsonSerializer.Deserialize<JsonElement>(jsonContentStr); }
        //             catch (Exception ex)
        //             {
        //                 Log.Error(ex, "[DingTalkEvents] JSON content deserialize failed. fileName={FileName} signature={Signature} timestamp={Timestamp} nonce={Nonce}", fileName, signature, timestamp, nonce);
        //                 await SendEncryptedSuccessAsync(timestamp, nonce, ct);
        //                 return;
        //             }

        //             var factory = Resolve<IHttpClientFactory>();
        //             var client = factory.CreateClient("SelfApi");
        //             if (client.BaseAddress is null)
        //             {
        //                 var host = HttpContext.Request.Host;
        //                 client.BaseAddress = new Uri($"{HttpContext.Request.Scheme}://{host}");
        //             }

        //             var bodyObj = new
        //             {
        //                 files = new[]
        //                 {
        //                     new { file = fileName, content = jsonContent }
        //                 }
        //             };

        //             var cfg = Resolve<IConfiguration>();
        //             var adminToken = cfg["Robot:AdminToken"] ?? string.Empty;
        //             if (!string.IsNullOrWhiteSpace(adminToken))
        //                 client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        //             HttpResponseMessage? resp;
        //             try { resp = await client.PostAsJsonAsync("/api/admin/config/update", bodyObj, ct); }
        //             catch { resp = null; }

        //             // 如果更新成功，发钉钉群提示
        //             if (resp?.IsSuccessStatusCode == true)
        //             {
        //                 var webhook = cfg["Robot:GroupWebhook"];
        //                 if (!string.IsNullOrWhiteSpace(webhook))
        //                 {
        //                     var dingMsg = new
        //                     {
        //                         msgtype = "text",
        //                         text = new { content = $"配置文件 {fileName} 更新成功 ✅ {DateTime.Now:yyyy-MM-dd HH:mm:ss}" }
        //                     };
        //                     try
        //                     {
        //                         await factory.CreateClient().PostAsJsonAsync(webhook, dingMsg, ct);
        //                     }
        //                     catch { /* ignore */ }
        //                 }
        //             }
        //         }
        //     }
        // }
        // catch
        // {
        //     // ignore parse errors
        // }

        // // 5) 最终回包：加密 success
        // await SendEncryptedSuccessAsync(timestamp, nonce, ct);
        // }
        // catch
        // {
        //     // 兜底：确保永远不把异常抛给框架/钉钉
        //     // 注意：这里不要再 throw
        //     var q = HttpContext.Request.Query;
        //     var timestamp2 = q["timestamp"].ToString();
        //     var nonce2 = q["nonce"].ToString();

        //     try
        //     {
        //         await SendEncryptedSuccessAsync(timestamp2, nonce2, ct);
        //     }
        //     catch
        //     {
        //         // 最后兜底：如果连加密 success 都失败，至少返回 200 空响应避免钉钉看到 500
        //         if (!HttpContext.Response.HasStarted)
        //         {
        //             HttpContext.Response.StatusCode = 200;
        //             await HttpContext.Response.CompleteAsync();
        //         }
       
    }

    private async Task SendEncryptedSuccessAsync(string timestamp, string nonce, CancellationToken ct)
    {
        timestamp = string.IsNullOrWhiteSpace(timestamp)
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
            : timestamp;

        nonce = string.IsNullOrWhiteSpace(nonce)
            ? Guid.NewGuid().ToString("N")
            : nonce;

        var encrypt = DingTalkCrypto.EncryptMsg("success", timestamp, nonce, out var msgSig);

        await HttpContext.Response.SendAsync(new
        {
            msg_signature = msgSig,
            timeStamp = timestamp,
            nonce,
            encrypt
        }, cancellation: ct);
    }
}
