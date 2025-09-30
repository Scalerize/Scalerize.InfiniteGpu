using MediatR;
namespace InfiniteGPU.Backend.Features.Auth.Commands;

public record RegisterCommand(
    string UserName,
    string Email,
    string Password
) : IRequest<string>;