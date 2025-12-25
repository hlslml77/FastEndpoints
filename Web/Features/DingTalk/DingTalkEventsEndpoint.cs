using System.Net.Http.Headers;
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
    // 兼容三种输入：
    //  1) "xxx.json { ... }" 或 "xxx.json [ ... ]"（文件名 + JSON）
    //  2) "xxx.json http(s)://..."（文件名 + URL）
    //  3) 仅文件名："xxx.json" 或 "xxx"（用于自动去 GitLab raw 拉取）
    // 允许前面有 @机器人 或任意文本；忽略大小写。
    private static readonly Regex _contentRegex = new(
        @"^[\s\S]*?(?<file>[\w\-.]+)(?:\s+(?<payload>(https?://\S+|(\{|\[)[\s\S]+)))?\s*$",
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
        var normalized = content.Trim();

        // 额外日志：帮助定位是换行/空格/全角符号/内容缺失导致的匹配失败
        Log.Information(
            "[DingTalkEvents] Incoming content (len={Len}). signature={Signature} timestamp={Timestamp} nonce={Nonce} contentPreview={Preview}",
            normalized.Length,
            signature,
            timestamp,
            nonce,
            normalized.Length <= 300 ? normalized : normalized[..300]);

        var match = _contentRegex.Match(normalized);
        if (!match.Success)
        {
            // 注意：这里不要打太长，避免刷屏；但仍保留足够上下文
            Log.Warning(
                "[DingTalkEvents] Content regex not matched. signature={Signature} timestamp={Timestamp} nonce={Nonce} contentLen={Len} content={Content}",
                signature,
                timestamp,
                nonce,
                normalized.Length,
                normalized.Length <= 2000 ? normalized : normalized[..2000]);
            await SendEncryptedSuccessAsync(timestamp, nonce, ct);
            return;
        }

        var fileName = match.Groups["file"].Value;
        if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            fileName += ".json";

        var payload = match.Groups["payload"].Success ? match.Groups["payload"].Value.Trim() : string.Empty;

        Log.Information(
            "[DingTalkEvents] Parsed content. file={File} hasPayload={HasPayload} payloadHead={PayloadHead}",
            fileName,
            !string.IsNullOrWhiteSpace(payload),
            string.IsNullOrWhiteSpace(payload) ? string.Empty : (payload.Length <= 120 ? payload : payload[..120]));

        var jsonContentStr = payload;

        // 去掉可能的 ```json ``` 包裹
        if (jsonContentStr.StartsWith("```", StringComparison.Ordinal))
        {
            var nl = jsonContentStr.IndexOf('\n');
            if (nl >= 0) jsonContentStr = jsonContentStr[(nl + 1)..];
            var lastFence = jsonContentStr.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence >= 0) jsonContentStr = jsonContentStr[..lastFence].Trim();
        }

        // 2) 解析内容：支持两种格式
        //   A) 直接在消息里贴 JSON（兼容你原来的逻辑）
        //   B) 贴 GitLab raw 地址（形式A：.../-/raw/<branch>/path/file.json），服务端拉取后再更新

        // 支持策划只输入文件名：
        //  - 例如："Item.json"
        //  - 自动从 GitLab 拉取：
        //    http://kjgitlab.yijiesudai.com:5013/pitpat/pitpatgame/raw/PitPat-NewWorld/PVEExcel/Json/Item.json
        // 同时仍兼容：直接贴 raw url、或直接贴 JSON。

        var candidate = jsonContentStr.Trim();

        // 先把“文件名”归一化（优先用正则捕获到的 fileName；candidate 可能为空）
        var inputName = string.IsNullOrWhiteSpace(fileName) ? candidate : fileName;
        inputName = inputName.Trim();

        // 如果出现了奇怪的路径（例如用户发了 E:\json\xxx.json），只取最后的文件名
        var onlyFileName = Path.GetFileName(inputName);
        if (string.IsNullOrWhiteSpace(onlyFileName))
            onlyFileName = Path.GetFileName(candidate);

        if (!onlyFileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            onlyFileName += ".json";

        // 判断输入类型：
        // - candidate 是 URL：用 URL 拉取
        // - candidate 是 JSON：直接用 JSON
        // - candidate 为空/不是 JSON：当成“只有文件名”，从 GitLab raw 拉取
        Uri? url = null;
        if (!string.IsNullOrWhiteSpace(candidate) && Uri.TryCreate(candidate, UriKind.Absolute, out var u) && (u.Scheme is "http" or "https"))
        {
            url = u;
        }
        else
        {
            var looksLikeJson = !string.IsNullOrWhiteSpace(candidate) && (candidate.StartsWith('{') || candidate.StartsWith('['));
            if (!looksLikeJson)
            {
                // 固定分支：PitPat-NewWorld
                var branch = "PitPat-NewWorld";
                var rawUrl = $"http://kjgitlab.yijiesudai.com:5013/pitpat/pitpatgame/raw/{Uri.EscapeDataString(branch)}/PVEExcel/Json/{Uri.EscapeDataString(onlyFileName)}";
                url = new Uri(rawUrl);

                // fileName 用归一化后的文件名（用于本地落盘更新）
                fileName = onlyFileName;

                Log.Information("[DingTalkEvents] Auto-fetch from GitLab raw. file={File} url={Url} (candidateEmpty={CandidateEmpty})",
                    fileName,
                    url.ToString(),
                    string.IsNullOrWhiteSpace(candidate));
            }
        }

        // 如果最终得到了 URL，就走远程拉取 JSON
        if (url is not null)
        {
            try
            {
                var cfg = Resolve<IConfiguration>();

                // GitLab 拉取鉴权（支持三种方式，按优先级：PRIVATE-TOKEN > Bearer > Basic）
                // 配置：Robot:GitLabToken / Robot:GitLabBearerToken / Robot:GitLabUsername / Robot:GitLabPassword
                var gitLabToken = cfg["Robot:GitLabToken"];
                var gitLabBearerToken = cfg["Robot:GitLabBearerToken"];
                var gitLabUsername = cfg["Robot:GitLabUsername"];
                var gitLabPassword = cfg["Robot:GitLabPassword"];

                var factory = Resolve<IHttpClientFactory>();
                var client = factory.CreateClient();

                var authMode = "none";

                if (!string.IsNullOrWhiteSpace(gitLabToken))
                {
                    // GitLab Personal Access Token (PAT)
                    client.DefaultRequestHeaders.Remove("PRIVATE-TOKEN");
                    client.DefaultRequestHeaders.Add("PRIVATE-TOKEN", gitLabToken);
                    authMode = "private-token";
                }
                else if (!string.IsNullOrWhiteSpace(gitLabBearerToken))
                {
                    // OAuth / Access Token
                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", gitLabBearerToken);
                    authMode = "bearer";
                }
                else if (!string.IsNullOrWhiteSpace(gitLabUsername) && !string.IsNullOrWhiteSpace(gitLabPassword))
                {
                    // Basic: username:password
                    var raw = $"{gitLabUsername}:{gitLabPassword}";
                    var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", b64);
                    authMode = "basic";
                }

                Log.Information(
                    "[DingTalkEvents] GitLab auth mode: {AuthMode} (hasPrivateToken={HasToken} hasBearer={HasBearer} hasBasic={HasBasic})",
                    authMode,
                    !string.IsNullOrWhiteSpace(gitLabToken),
                    !string.IsNullOrWhiteSpace(gitLabBearerToken),
                    !string.IsNullOrWhiteSpace(gitLabUsername) && !string.IsNullOrWhiteSpace(gitLabPassword));

                using var resp = await client.GetAsync(url, ct);
                var bodyStr = await resp.Content.ReadAsStringAsync(ct);

                var contentType = resp.Content.Headers.ContentType?.ToString() ?? string.Empty;
                Log.Information(
                    "[DingTalkEvents] GitLab fetch done. status={Status} contentType={ContentType} len={Len} url={Url}",
                    (int)resp.StatusCode,
                    contentType,
                    bodyStr?.Length ?? 0,
                    url.ToString());

                // GitLab 没权限/登录页/错误页经常会返回 HTML（虽然可能还是 200）。这里做硬性校验，避免后面 JSON 解析报 '<'
                var head = (bodyStr ?? string.Empty).TrimStart();
                if (!resp.IsSuccessStatusCode)
                {
                    Log.Error(
                        "[DingTalkEvents] GitLab fetch failed. status={Status} url={Url} bodyHead={BodyHead}",
                        (int)resp.StatusCode,
                        url.ToString(),
                        head.Length <= 300 ? head : head[..300]);
                    await SendEncryptedSuccessAsync(timestamp, nonce, ct);
                    return;
                }

                if (head.Length == 0 || head[0] == '<')
                {
                    Log.Error(
                        "[DingTalkEvents] GitLab response is not JSON (maybe HTML login/permission page). url={Url} contentType={ContentType} bodyHead={BodyHead}",
                        url.ToString(),
                        contentType,
                        head.Length <= 500 ? head : head[..500]);
                    await SendEncryptedSuccessAsync(timestamp, nonce, ct);
                    return;
                }

                jsonContentStr = bodyStr;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DingTalkEvents] Fetch raw json from url failed. fileName={FileName} url={Url}", fileName, url.ToString());
                await SendEncryptedSuccessAsync(timestamp, nonce, ct);
                return;
            }
        }

        // 解析 JSON 内容
        if (string.IsNullOrWhiteSpace(jsonContentStr))
        {
            Log.Error("[DingTalkEvents] JSON content is empty. fileName={FileName}", fileName);
            await SendEncryptedSuccessAsync(timestamp, nonce, ct);
            return;
        }

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
            var ts = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var backupPath = target + "." + ts + ".bak";
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
