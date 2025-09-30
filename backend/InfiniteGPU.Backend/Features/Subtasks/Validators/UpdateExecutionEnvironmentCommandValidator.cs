using FluentValidation;
using InfiniteGPU.Backend.Features.Subtasks.Commands;

namespace InfiniteGPU.Backend.Features.Subtasks.Validators;

public sealed class UpdateExecutionEnvironmentCommandValidator : AbstractValidator<UpdateExecutionEnvironmentCommand>
{
    public UpdateExecutionEnvironmentCommandValidator()
    {
        RuleFor(x => x.SubtaskId)
            .NotEmpty();

        RuleFor(x => x.ProviderUserId)
            .NotEmpty();

        RuleFor(x => x.Update)
            .NotNull()
            .Must(update => update.HasAnyChanges)
            .WithMessage("At least one execution environment property must be provided.");
    }
}