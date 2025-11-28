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
            .GreaterThan(0).WithMessage("Distance must be a positive number.");

        RuleFor(x => x.Calorie)
            .GreaterThan(0).WithMessage("Calorie must be a positive number.");
    }
}

