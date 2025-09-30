using MediatR;
using InfiniteGPU.Backend.Shared.Models;
using TaskStatusEnum = InfiniteGPU.Backend.Shared.Models.TaskStatus;

namespace InfiniteGPU.Backend.Features.Tasks.Queries;

public record GetTasksQuery(
    string UserId,
    TaskStatusEnum? StatusFilter = null
) : IRequest<List<TaskDto>>;