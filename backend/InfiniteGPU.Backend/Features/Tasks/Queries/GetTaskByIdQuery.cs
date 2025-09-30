using MediatR;
using InfiniteGPU.Backend.Shared.Models;

namespace InfiniteGPU.Backend.Features.Tasks.Queries;

public record GetTaskByIdQuery(
    Guid Id,
    string UserId
) : IRequest<TaskDto>;