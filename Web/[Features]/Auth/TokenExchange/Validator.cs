using FluentValidation;

namespace Web.Auth.TokenExchange;

/// <summary>
/// Token交换请求验证器
/// </summary>
public class Validator : Validator<TokenExchangeRequest>
{
    public Validator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("userId is required");

        // AppToken 可选；若传则做基本格式校验
        When(x => !string.IsNullOrWhiteSpace(x.AppToken), () =>
        {
            RuleFor(x => x.AppToken!)
                .Must(BeValidJwtFormat)
                .WithMessage("Invalid JWT token format");
        });
    }

    private bool BeValidJwtFormat(string token)
    {
        // JWT格式验证：应该有3个部分，用.分隔
        var parts = token.Split('.');
        return parts.Length == 3;
    }
}

