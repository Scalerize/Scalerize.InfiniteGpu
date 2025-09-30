using MediatR;

namespace InfiniteGPU.Backend.Features.Auth.Commands;

public sealed record ResetPasswordCommand(
    string Email,
    string Token,
    string NewPassword
) : IRequest;