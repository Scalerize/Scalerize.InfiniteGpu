using System;
using System.Threading;
using System.Threading.Tasks;
using InfiniteGPU.Backend.Data;
using InfiniteGPU.Backend.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InfiniteGPU.Backend.Shared.Services;

public sealed class ApiKeyAuthenticationService
{
    public const string ApiKeyHeaderName = "X-Api-Key";

    private readonly AppDbContext _context;
    private readonly ILogger<ApiKeyAuthenticationService> _logger;

    public ApiKeyAuthenticationService(
        AppDbContext context,
        ILogger<ApiKeyAuthenticationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public static string? ReadApiKeyFromHeaders(IHeaderDictionary headers)
    {
        if (headers.TryGetValue(ApiKeyHeaderName, out var values))
        {
            var candidate = values.ToString();
            return string.IsNullOrWhiteSpace(candidate) ? null : candidate.Trim();
        }

        return null;
    }

    public async Task<ApiKeyAuthenticationResult?> AuthenticateAsync(
        string? apiKeyValue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKeyValue))
        {
            return null;
        }

        var apiKey = await _context.ApiKeys
            .Include(k => k.User)
            .FirstOrDefaultAsync(k => k.Key == apiKeyValue && k.IsActive, cancellationToken);

        if (apiKey is null || apiKey.User is null || !apiKey.User.IsActive)
        {
            _logger.LogWarning("Invalid API key");
            return null;
        }

        apiKey.LastUsedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return new ApiKeyAuthenticationResult(apiKey, apiKey.User);
    }
}

public sealed record ApiKeyAuthenticationResult(ApiKey ApiKey, ApplicationUser User);