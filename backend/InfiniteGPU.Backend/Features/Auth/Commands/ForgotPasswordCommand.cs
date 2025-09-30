using MediatR;

namespace InfiniteGPU.Backend.Features.Auth.Commands;

public sealed record ForgotPasswordCommand(string Email) : IRequest;