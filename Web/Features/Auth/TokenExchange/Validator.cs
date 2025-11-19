using FluentValidation;

namespace Web.Features.Auth.TokenExchange;

/// <summary>
/// Token交换请求验证器
/// </summary>
public class Validator : Validator<TokenExchangeRequest>
{
    public Validator()
    {
        RuleFor(x => x.AppToken)
            .NotEmpty()
            .WithMessage("APP Token is required")
            .Must(BeValidJwtFormat)
            .WithMessage("Invalid JWT token format");
    }

    private bool BeValidJwtFormat(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        // JWT格式验证：应该有3个部分，用.分隔
        var parts = token.Split('.');
        return parts.Length == 3;
    }
}
