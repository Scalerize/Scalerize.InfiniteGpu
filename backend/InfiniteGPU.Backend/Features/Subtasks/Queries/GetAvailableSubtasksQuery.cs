using InfiniteGPU.Backend.Shared.Models;
using MediatR;

namespace InfiniteGPU.Backend.Features.Subtasks.Queries;

public sealed record GetAvailableSubtasksQuery(string ProviderUserId) : IRequest<IReadOnlyList<SubtaskDto>>;