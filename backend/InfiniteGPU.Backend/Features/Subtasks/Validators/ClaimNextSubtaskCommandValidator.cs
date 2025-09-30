using FluentValidation;
using InfiniteGPU.Backend.Features.Subtasks.Commands;

namespace InfiniteGPU.Backend.Features.Subtasks.Validators;

public sealed class ClaimNextSubtaskCommandValidator : AbstractValidator<ClaimNextSubtaskCommand>
{
    public ClaimNextSubtaskCommandValidator()
    {
        RuleFor(x => x.ProviderUserId)
            .NotEmpty();
    }
}