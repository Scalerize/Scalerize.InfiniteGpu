using MediatR;

namespace InfiniteGPU.Backend.Features.Auth.Commands;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<string>;