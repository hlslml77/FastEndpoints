namespace Web.Features.Auth.TokenExchange;

/// <summary>
/// Token交换请求模型
/// </summary>
public class TokenExchangeRequest
{
    /// <summary>
    /// APP生成的JWT Token
    /// </summary>
    public string AppToken { get; set; } = string.Empty;
}

/// <summary>
/// Token交换响应模型
/// </summary>
public class TokenExchangeResponse
{
    /// <summary>
    /// Web服务专用JWT Token
    /// </summary>
    public string WebToken { get; set; } = string.Empty;

    /// <summary>
    /// Token有效期（秒）
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Token类型
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 用户ID
    /// </summary>
    public string UserId { get; set; } = string.Empty;
}

/// <summary>
/// APP Token验证结果
/// </summary>
internal class TokenValidationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public List<string> Roles { get; set; } = new();
}

/// <summary>
/// APP服务验证响应模型
/// </summary>
internal class AppTokenValidationResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}
