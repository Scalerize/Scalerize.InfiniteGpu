using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.AspNetCore.Identity;
using InfiniteGPU.Backend.Data;
using InfiniteGPU.Backend.Data.Entities;
using InfiniteGPU.Backend.Features.Auth.Commands;
using InfiniteGPU.Backend.Shared.Services;

namespace InfiniteGPU.Backend.Features.Auth.Handlers;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, string>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly JwtService _jwtService;
    private readonly AppDbContext _context;

    public RegisterCommandHandler(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        JwtService jwtService,
        AppDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _jwtService = jwtService;
        _context = context;
    }

    public async Task<string> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email, 
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            throw new Exception(string.Join(", ", result.Errors.Select(e => e.Description)));
        }
  
        var apiKeyId = Guid.NewGuid();
        var apiKeyValue = GenerateApiKey(user.Id, apiKeyId);
        var apiKeyEntity = new ApiKey
        {
            Id = apiKeyId,
            UserId = user.Id,
            Prefix = ExtractPrefix(apiKeyValue),
            Key = apiKeyValue,
            Description = "Default requestor key",
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.ApiKeys.Add(apiKeyEntity);
        await _context.SaveChangesAsync(cancellationToken);

        return _jwtService.GenerateJwtToken(user.Id, user.UserName, user.Email);
    }

    private static string GenerateApiKey(string userId, Guid apiKeyId)
    {
        var sanitizedUserSegment = new string(userId.Where(char.IsLetterOrDigit).Take(8).ToArray());
        if (sanitizedUserSegment.Length < 8)
        {
            sanitizedUserSegment = sanitizedUserSegment.PadRight(8, '0');
        }

        var uniqueSegment = apiKeyId.ToString("N");
        return $"pk-{sanitizedUserSegment}-{uniqueSegment}";
    }

    private static string ExtractPrefix(string apiKey) =>
        apiKey.Length <= 16 ? apiKey : apiKey[..16];

}