using FastEndpoints;
using FastEndpoints.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;

namespace Web.Features.Auth.TokenExchange;

/// <summary>
/// Token交换端点 - 用APP Token换取Web服务专用Token
/// </summary>
[HttpPost("/auth/exchange"), AllowAnonymous]
public class Endpoint : Endpoint<TokenExchangeRequest, TokenExchangeResponse>
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public Endpoint(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public override async Task HandleAsync(TokenExchangeRequest req, CancellationToken ct)
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

            // 2. 生成Web服务专用的JWT Token (暂时跳过验证)
            var webToken = JwtBearer.CreateToken(o =>
            {
                o.SigningKey = _config["TokenKey"]!;
                o.ExpireAt = DateTime.UtcNow.AddHours(2); // Web token有效期2小时

                // 最大化权限配置 - 所有读写权限
                // 基础访问权限
                o.User.Permissions.Add("web_access");
                o.User.Permissions.Add("api_read");
                o.User.Permissions.Add("api_write");
                o.User.Permissions.Add("profile_update");
            });

            // 3. 返回Web Token
            Response = new TokenExchangeResponse
            {
                WebToken = webToken,
                ExpiresIn = 43200, // 6小时
                TokenType = "Bearer",
                UserId = "anonymous" // 暂时使用匿名用户
            };
        }
        catch (Exception ex)
        {
            AddError("Token exchange failed: " + ex.Message);
            ThrowError("Token exchange failed: " + ex.Message);
        }
    }

    private async Task<TokenValidationResult> ValidateAppTokenAsync(string appToken, CancellationToken ct)
    {
        try
        {
            // 方式1：如果知道APP的签名密钥，直接验证
            if (!string.IsNullOrEmpty(_config["AppPublicKey"]))
            {
                return await ValidateTokenWithKeyAsync(appToken, ct);
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
                UserId = validationData.UserId,
                Username = validationData.Username,
                Roles = validationData.Roles
            };
        }
        catch (Exception ex)
        {
            return new TokenValidationResult { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    private async Task<TokenValidationResult> ValidateTokenWithKeyAsync(string appToken, CancellationToken ct)
    {
        // 使用APP的公钥直接验证token
        // 这里简化处理，实际需要使用JWT库验证
        var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
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
        
        return new TokenValidationResult
        {
            IsValid = true,
            UserId = principal.FindFirst("sub")?.Value,
            Username = principal.FindFirst("name")?.Value,
            Roles = principal.FindAll("role").Select(c => c.Value).ToList()
        };
    }

    private static RsaSecurityKey CreateRsaSecurityKey(string publicKeyBase64)
    {
        var rsa = RSA.Create();
        rsa.ImportRSAPublicKey(Convert.FromBase64String(publicKeyBase64), out _);
        return new RsaSecurityKey(rsa);
    }
}
