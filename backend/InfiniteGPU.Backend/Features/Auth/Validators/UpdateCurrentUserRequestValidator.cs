using FluentValidation;
using InfiniteGPU.Backend.Features.Auth.Models;

namespace InfiniteGPU.Backend.Features.Auth.Validators;

public sealed class UpdateCurrentUserRequestValidator : AbstractValidator<UpdateCurrentUserRequest>
{
    public UpdateCurrentUserRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .When(x => x.FirstName is not null, ApplyConditionTo.CurrentValidator)
            .WithMessage("First name must not exceed 100 characters.")
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 0)
            .WithMessage("First name cannot be only whitespace.");

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .When(x => x.LastName is not null, ApplyConditionTo.CurrentValidator)
            .WithMessage("Last name must not exceed 100 characters.")
            .Must(value => string.IsNullOrWhiteSpace(value) || value.Trim().Length > 0)
            .WithMessage("Last name cannot be only whitespace.");

        RuleFor(x => x)
            .Must(request => request.FirstName is not null || request.LastName is not null)
            .WithMessage("At least one of firstName or lastName must be provided.");
    }
}