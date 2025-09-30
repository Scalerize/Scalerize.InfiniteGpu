using FluentValidation;
using InfiniteGPU.Backend.Features.Subtasks.Commands;

namespace InfiniteGPU.Backend.Features.Subtasks.Validators;

public sealed class CompleteSubtaskCommandValidator : AbstractValidator<CompleteSubtaskCommand>
{
    public CompleteSubtaskCommandValidator()
    {
        RuleFor(x => x.SubtaskId)
            .NotEmpty();

        RuleFor(x => x.ProviderUserId)
            .NotEmpty();

        RuleFor(x => x.ResultsJson)
            .NotEmpty();
    }
}