using InfiniteGPU.Backend.Data.Entities;
using InfiniteGPU.Backend.Features.Auth.Commands;
using InfiniteGPU.Backend.Features.Auth.Models;
using MediatR;
using Microsoft.AspNetCore.Identity;

namespace InfiniteGPU.Backend.Features.Auth.Handlers;

public sealed class UpdateCurrentUserCommandHandler
    : IRequestHandler<UpdateCurrentUserCommand, UpdateCurrentUserResponse>
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UpdateCurrentUserCommandHandler(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<UpdateCurrentUserResponse> Handle(
        UpdateCurrentUserCommand request,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        var targetFirstName = request.FirstName is null
            ? user.FirstName
            : NormalizeName(request.FirstName);

        var targetLastName = request.LastName is null
            ? user.LastName
            : NormalizeName(request.LastName);

        user.FirstName = targetFirstName;
        user.LastName = targetLastName;

        var identityResult = await _userManager.UpdateAsync(user);
        if (!identityResult.Succeeded)
        {
            var errors = string.Join(", ", identityResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to update user profile: {errors}");
        }

        return new UpdateCurrentUserResponse(
            user.Id,
            targetFirstName,
            targetLastName,
            user.Email);
    }

    private static string? NormalizeName(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }
}