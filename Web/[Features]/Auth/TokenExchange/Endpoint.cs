using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;

using Microsoft.Extensions.Caching.Memory;
using Serilog;
using Web.Auth;


namespace Web.Auth.TokenExchange;

/// <summary>
/// Token交换端点 - 受控后台签发（生产可用）：需要管理密钥与时间/nonce 校验
/// </summary>
[HttpPost("/auth/exchange"), AllowAnonymous]
public class Endpoint : Endpoint<TokenExchangeRequest, TokenExchangeResponse>
{
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;

    public Endpoint(IConfiguration config, IMemoryCache cache)
    {
        _config = config;
        _cache = cache;
    }

    public override async Task HandleAsync(TokenExchangeRequest req, CancellationToken ct)
    {
        try
        {
            var section = _config.GetSection("AuthExchange");
            var enabled = section.GetValue<bool>("Enabled");
            if (!enabled)
            {
                await HttpContext.Response.SendAsync(new { statusCode = 403, code = Web.Data.ErrorCodes.Auth.Forbidden, message = "auth exchange disabled" }, 403, cancellation: ct);
                return;
            }

            // 1) 校验管理密钥（固定时序比较）
            var adminKeyCfg = section["AdminKey"] ?? string.Empty;
            var adminKeyHdr = HttpContext.Request.Headers["X-Exchange-Key"].ToString();
            if (string.IsNullOrEmpty(adminKeyCfg) || string.IsNullOrEmpty(adminKeyHdr) || !FixedTimeEquals(adminKeyHdr, adminKeyCfg))
            {
                await HttpContext.Response.SendAsync(new { statusCode = 403, code = Web.Data.ErrorCodes.Auth.Forbidden, message = "forbidden" }, 403, cancellation: ct);
                return;
            }

            // 2) 可选：IP 白名单
            var allowedIPs = section.GetSection("AllowedIPs").Get<string[]>() ?? Array.Empty<string>();
            var remoteIP = HttpContext.Connection.RemoteIpAddress?.ToString();
            if (allowedIPs.Length > 0 && (remoteIP is null || !allowedIPs.Contains(remoteIP)))
            {
                await HttpContext.Response.SendAsync(new { statusCode = 403, code = Web.Data.ErrorCodes.Auth.Forbidden, message = "ip not allowed" }, 403, cancellation: ct);
                return;
            }

            // 3) 时间窗口 + nonce 防重放
            var tsHdr = HttpContext.Request.Headers["X-Timestamp"].ToString();
            var nonce = HttpContext.Request.Headers["X-Nonce"].ToString();
            if (!long.TryParse(tsHdr, out var tsMs))
            {
                await HttpContext.Response.SendAsync(new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "invalid timestamp" }, 400, cancellation: ct);
                return;
            }
            var maxSkew = section.GetValue<int>("MaxSkewSeconds", 300);
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Math.Abs(nowMs - tsMs) > maxSkew * 1000L)
            {
                await HttpContext.Response.SendAsync(new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "timestamp skew too large" }, 400, cancellation: ct);
                return;
            }
            if (string.IsNullOrWhiteSpace(nonce) || nonce.Length < 12)
            {
                await HttpContext.Response.SendAsync(new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "invalid nonce" }, 400, cancellation: ct);
                return;
            }
            var cacheKey = $"auth:ex:nonce:{nonce}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                await HttpContext.Response.SendAsync(new { statusCode = 409, code = Web.Data.ErrorCodes.Common.Conflict, message = "replay detected" }, 409, cancellation: ct);
                return;
            }
            _cache.Set(cacheKey, 1, TimeSpan.FromMinutes(5));

            // 4) 校验 userId 并签发最小权限短效 Web Token
            var userId = (req.UserId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userId) || !long.TryParse(userId, out _))
            {
                await HttpContext.Response.SendAsync(new { statusCode = 400, code = Web.Data.ErrorCodes.Common.BadRequest, message = "userId is required" }, 400, cancellation: ct);
                return;
            }

            var wantAdmin = string.Equals(HttpContext.Request.Headers["X-Admin"].ToString(), "1", StringComparison.OrdinalIgnoreCase);

            var webToken = JwtBearer.CreateToken(o =>
            {
                o.SigningKey = _config["TokenKey"]!;
                o.ExpireAt = DateTime.UtcNow.AddHours(6); // 6 小时
                o.User.Claims.Add(new System.Security.Claims.Claim("sub", userId));
                o.User.Permissions.Add("web_access"); // 最小权限
                if (wantAdmin)
                    o.User.Roles.Add(Role.Admin);
            });

            await HttpContext.Response.SendAsync(new TokenExchangeResponse
            {
                WebToken = webToken,
                ExpiresIn = 21600,
                TokenType = "Bearer",
                UserId = userId
            }, 200, cancellation: ct);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Token exchange failed");
            var errorBody = new { statusCode = 500, code = Web.Data.ErrorCodes.Common.InternalError, message = "Token exchange failed" };
            await HttpContext.Response.SendAsync(errorBody, 500, cancellation: ct);
        }
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = System.Text.Encoding.UTF8.GetBytes(a);
        var bb = System.Text.Encoding.UTF8.GetBytes(b);
        return CryptographicOperations.FixedTimeEquals(
            ba.Length == bb.Length ? ba : ba.Concat(new byte[bb.Length - ba.Length]).ToArray(),
            bb.Length == ba.Length ? bb : bb.Concat(new byte[ba.Length - bb.Length]).ToArray());
    }
}

