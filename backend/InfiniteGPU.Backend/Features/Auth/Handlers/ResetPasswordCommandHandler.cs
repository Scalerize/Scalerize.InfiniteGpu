using MediatR;
using Microsoft.AspNetCore.Identity;
using InfiniteGPU.Backend.Data.Entities;
using InfiniteGPU.Backend.Features.Auth.Commands;

namespace InfiniteGPU.Backend.Features.Auth.Handlers;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        UserManager<ApplicationUser> userManager,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            throw new InvalidOperationException("Unable to reset password with provided credentials.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
            _logger.LogWarning("Failed password reset for user {UserId}: {Errors}", user.Id, errorMessages);
            throw new InvalidOperationException(errorMessages);
        }

        await _userManager.UpdateSecurityStampAsync(user);
    }
}