using FluentValidation;
using InfiniteGPU.Backend.Features.Subtasks.Commands;

namespace InfiniteGPU.Backend.Features.Subtasks.Validators;

public sealed class AcceptSubtaskCommandValidator : AbstractValidator<AcceptSubtaskCommand>
{
    public AcceptSubtaskCommandValidator()
    {
        RuleFor(x => x.SubtaskId)
            .NotEmpty();

        RuleFor(x => x.ProviderUserId)
            .NotEmpty();
    }
}