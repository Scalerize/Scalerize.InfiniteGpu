using MediatR;
using InfiniteGPU.Backend.Features.Auth.Models;

namespace InfiniteGPU.Backend.Features.Auth.Commands;

public sealed record UpdateCurrentUserCommand(
    string UserId,
    string? FirstName,
    string? LastName
) : IRequest<UpdateCurrentUserResponse>;