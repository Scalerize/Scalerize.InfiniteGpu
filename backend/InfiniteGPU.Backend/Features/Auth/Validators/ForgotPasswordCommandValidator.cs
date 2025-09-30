using FluentValidation;
using InfiniteGPU.Backend.Features.Auth.Commands;

namespace InfiniteGPU.Backend.Features.Auth.Validators;

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");
    }
}