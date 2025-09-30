using MediatR;

namespace InfiniteGPU.Backend.Features.Tasks.Commands;

public record AssignSubtaskCommand(
    Guid TaskId,
    string ProviderUserId
) : IRequest<bool>;