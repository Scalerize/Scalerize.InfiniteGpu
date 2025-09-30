using MediatR;
using InfiniteGPU.Backend.Features.Tasks.Models;

namespace InfiniteGPU.Backend.Features.Tasks.Queries;

public record GetRequestorIntakeQuery(string UserId) : IRequest<RequestorIntakeDto>;