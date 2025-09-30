using System.Text;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using InfiniteGPU.Backend.Data.Entities;
using InfiniteGPU.Backend.Features.Auth.Commands;
using InfiniteGPU.Backend.Shared.Options;
using InfiniteGPU.Backend.Shared.Services;

namespace InfiniteGPU.Backend.Features.Auth.Handlers;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly FrontendOptions _frontendOptions;
    private readonly ILogger<ForgotPasswordCommandHandler> _logger;

    public ForgotPasswordCommandHandler(
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IOptions<FrontendOptions> frontendOptions,
        ILogger<ForgotPasswordCommandHandler> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _frontendOptions = frontendOptions.Value;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return;
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            _logger.LogInformation("Password reset requested for non-existent or inactive user {Email}", email);
            return;
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetLink = BuildResetLink(user.Email!, token);
        var textBody = BuildEmailTextBody(resetLink, token);

        try
        {
            await _emailSender.SendAsync(
                user.Email!,
                "Reset your InfiniteGPU password",
                textBody,
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email for user {UserId}", user.Id);
            throw new InvalidOperationException("Failed to send password reset email.");
        }
    }

    private string BuildResetLink(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(_frontendOptions.PasswordResetUrl))
        {
            _logger.LogWarning("Frontend password reset URL is not configured. Token will be delivered without link.");
            return string.Empty;
        }

        var separator = _frontendOptions.PasswordResetUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{_frontendOptions.PasswordResetUrl}{separator}email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}";
    }

    private static string BuildEmailTextBody(string resetLink, string token)
    {
        var builder = new StringBuilder();
        builder.AppendLine("We received a request to reset your InfiniteGPU account password.");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(resetLink))
        {
            builder.AppendLine("To reset your password, click the link below or paste it into your browser:");
            builder.AppendLine(resetLink);
            builder.AppendLine();
        }

        builder.AppendLine("If the link does not work, you can use the token below with the reset form:");
        builder.AppendLine(token);
        builder.AppendLine();
        builder.AppendLine("If you did not request a password reset, you can safely ignore this email.");

        return builder.ToString();
    }
}