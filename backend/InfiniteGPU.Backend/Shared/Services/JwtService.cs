using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InfiniteGPU.Backend.Shared.Services;

public class JwtService
{
    private readonly string _key;
    private readonly string _issuer;
    private readonly string _audience;

    public JwtService(IConfiguration configuration)
    {
        _key = configuration["Jwt:Key"] ?? throw new ArgumentNullException("JWT Key is required");
        _issuer = configuration["Jwt:Issuer"] ?? throw new ArgumentNullException("JWT Issuer is required");
        _audience = configuration["Jwt:Audience"] ?? throw new ArgumentNullException("JWT Audience is required");
    }

    public string GenerateJwtToken(string userId, string? userName, string? email)
    {
        var resolvedName = string.IsNullOrWhiteSpace(userName) ? userId : userName;

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(JwtRegisteredClaimNames.UniqueName, resolvedName),
            new Claim(ClaimTypes.Name, resolvedName)
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Email, email));
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.Now.AddDays(7),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}