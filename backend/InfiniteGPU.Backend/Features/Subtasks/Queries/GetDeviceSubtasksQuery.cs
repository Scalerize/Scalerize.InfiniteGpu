using InfiniteGPU.Backend.Shared.Models;
using MediatR;

namespace InfiniteGPU.Backend.Features.Subtasks.Queries;

public sealed record GetDeviceSubtasksQuery(string ProviderUserId, string DeviceIdentifier) : IRequest<IReadOnlyList<SubtaskDto>>;