using InfiniteGPU.Backend.Shared.Models;
using MediatR;

namespace InfiniteGPU.Backend.Features.Subtasks.Commands;

public sealed record ClaimNextSubtaskCommand(string ProviderUserId, Guid DeviceId) : IRequest<SubtaskDto?>;
