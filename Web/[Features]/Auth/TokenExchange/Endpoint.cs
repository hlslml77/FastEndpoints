using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;

namespace Web.Auth.TokenExchange;

/// <summary>
/// Token交换端点 - 用APP Token换取Web服务专用Token
/// </summary>
[HttpPost("/auth/exchange"), AllowAnonymous]
public class Endpoint : Endpoint<TokenExchangeRequest, TokenExchangeResponse>
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Endpoint> _logger;

    public Endpoint(IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<Endpoint> logger)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public override Task HandleAsync(TokenExchangeRequest req, CancellationToken ct)
    {
        try
        {
            // 1. 验证APP Token的有效性 (暂时注释掉)
            /*
            var validationResult = await ValidateAppTokenAsync(req.AppToken, ct);
            if (!validationResult.IsValid)
            {
                AddError("Invalid APP token: " + validationResult.ErrorMessage);
                await SendErrorsAsync();
                return;
            }
            */

            // 2. 使用客户端传入的UserId作为JWT的sub
            var userId = (req.UserId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning("Token exchange failed: missing userId");
                ThrowError("userId is required");
                return Task.CompletedTask;
            }

            // 3. 生成Web服务专用的JWT Token，并把用户ID写入sub
            var webToken = JwtBearer.CreateToken(o =>
            {
                o.SigningKey = _config["TokenKey"]!;
                o.ExpireAt = DateTime.UtcNow.AddHours(6); // Web token有效期6小时

                // 用户ID（用于服务端从JWT解析）
                o.User.Claims.Add(new System.Security.Claims.Claim("sub", userId));

                // 权限
                o.User.Permissions.Add("web_access");
                o.User.Permissions.Add("api_read");
                o.User.Permissions.Add("api_write");
                o.User.Permissions.Add("profile_update");
            });

            // 4. 返回Web Token
            Response = new TokenExchangeResponse
            {
                WebToken = webToken,
                ExpiresIn = 43200, // 6小时
                TokenType = "Bearer",
                UserId = userId
            };

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token exchange failed");
            ThrowError("Token exchange failed");
            return Task.CompletedTask;
        }
    }

    private async Task<TokenValidationResult> ValidateAppTokenAsync(string appToken, CancellationToken ct)
    {
        try
        {
            // 方式1：如果知道APP的签名密钥，直接验证
            if (!string.IsNullOrEmpty(_config["AppPublicKey"]))
            {
                return await ValidateTokenWithKeyAsync(appToken);
            }

            // 方式2：调用APP服务的验证接口
            var httpClient = _httpClientFactory.CreateClient("AppService");
            var response = await httpClient.PostAsJsonAsync("/auth/validate", new { token = appToken }, ct);

            if (!response.IsSuccessStatusCode)
            {
                return new TokenValidationResult { IsValid = false, ErrorMessage = "APP token validation failed" };
            }

            var validationData = await response.Content.ReadFromJsonAsync<AppTokenValidationResponse>(ct);
            return new TokenValidationResult
            {
                IsValid = true,
                UserId = validationData?.UserId,
                Username = validationData?.Username,
                Roles = validationData?.Roles ?? new List<string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ValidateAppTokenAsync failed");
            return new TokenValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    private Task<TokenValidationResult> ValidateTokenWithKeyAsync(string appToken)
    {
        // 使用APP的公钥直接验证token
        // 这里简化处理，实际需要使用JWT库验证
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _config["AppIssuer"],
            ValidateAudience = true,
            ValidAudience = _config["AppAudience"],
            ValidateLifetime = true,
            IssuerSigningKey = CreateRsaSecurityKey(_config["AppPublicKey"]!),
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(appToken, validationParameters, out var validatedToken);

        return Task.FromResult(new TokenValidationResult
        {
            IsValid = true,
            UserId = principal.FindFirst("sub")?.Value,
            Username = principal.FindFirst("name")?.Value,
            Roles = principal.FindAll("role").Select(c => c.Value).ToList()
        });
    }

    private static RsaSecurityKey CreateRsaSecurityKey(string publicKeyBase64)
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
        return new RsaSecurityKey(rsa);
    }
}

