using MediatR;
using Microsoft.AspNetCore.Identity;
using InfiniteGPU.Backend.Data.Entities;
using InfiniteGPU.Backend.Features.Auth.Commands;
using InfiniteGPU.Backend.Shared.Services;

namespace InfiniteGPU.Backend.Features.Auth.Handlers;

public class LoginCommandHandler : IRequestHandler<LoginCommand, string>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtService _jwtService;

    public LoginCommandHandler(
        UserManager<ApplicationUser> userManager,
        JwtService jwtService)
    {
        _userManager = userManager;
        _jwtService = jwtService;
    }

    public async Task<string> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User account is not active");
        }

        return _jwtService.GenerateJwtToken(user.Id, user.UserName, user.Email);
    }
}