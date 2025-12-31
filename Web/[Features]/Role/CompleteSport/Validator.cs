using FastEndpoints;
using FluentValidation;
using RoleApi;

namespace RoleApi.CompleteSport;

/// <summary>
/// Validator for the CompleteSport request
/// </summary>
public class Validator : Validator<CompleteSportRequest>
{
    public Validator()
    {
        RuleFor(x => x.DeviceType)
            .GreaterThanOrEqualTo(0).WithMessage("A valid DeviceType is required.");

        RuleFor(x => x.Distance)
            .GreaterThanOrEqualTo(0).WithMessage("Distance must be a non-negative number.");

        RuleFor(x => x.Calorie)
            .GreaterThanOrEqualTo(0).WithMessage("Calorie must be a non-negative number.");
    }
}

