using InfiniteGPU.Backend.Data;
using InfiniteGPU.Backend.Data.Entities;
using InfiniteGPU.Backend.Features.Finance.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Stripe;

namespace InfiniteGPU.Backend.Features.Finance.Handlers;

public sealed class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, ProcessPaymentResult>
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProcessPaymentResult> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Get user
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

            if (user == null)
            {
                return new ProcessPaymentResult(false, null, "User not found");
            }

            // Initialize Stripe
            var stripeSecretKey = _configuration["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(stripeSecretKey))
            {
                _logger.LogError("Stripe secret key not configured");
                return new ProcessPaymentResult(false, null, "Payment system not configured");
            }

            StripeConfiguration.ApiKey = stripeSecretKey;

            // Create PaymentIntent with Stripe with 3D Secure support
            var paymentIntentService = new PaymentIntentService();
            var paymentIntentOptions = new PaymentIntentCreateOptions
            {
                Amount = (long)(request.Amount * 100), // Convert to cents
                Currency = "usd",
                PaymentMethod = request.StripePaymentMethodId,
                Confirm = true,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                    AllowRedirects = "always" // Allow redirects for 3D Secure authentication
                },
                // Set return URL for 3D Secure redirects
                ReturnUrl = _configuration["Frontend:BaseUrl"] ?? "http://localhost:5173/finance",
                Metadata = new Dictionary<string, string>
                {
                    { "user_id", request.UserId },
                    { "type", "balance_topup" }
                },
                // Request 3D Secure when supported
                PaymentMethodOptions = new PaymentIntentPaymentMethodOptionsOptions
                {
                    Card = new PaymentIntentPaymentMethodOptionsCardOptions
                    {
                        RequestThreeDSecure = "automatic" // Request 3DS when available
                    }
                }
            };

            PaymentIntent paymentIntent;
            try
            {
                paymentIntent = await paymentIntentService.CreateAsync(paymentIntentOptions, cancellationToken: cancellationToken);
            }
            catch (StripeException stripeEx)
            {
                _logger.LogError(stripeEx, "Stripe payment failed for user {UserId}", request.UserId);
                return new ProcessPaymentResult(false, null, $"Payment failed: {stripeEx.Message}");
            }

            // Check payment status - with 3DS, status might be "requires_action"
            if (paymentIntent.Status == "requires_action" || paymentIntent.Status == "requires_source_action")
            {
                // Payment requires additional authentication (3D Secure)
                // Return client_secret so frontend can complete authentication
                _logger.LogInformation(
                    "Payment requires 3D Secure authentication. PaymentIntentId: {PaymentIntentId}, UserId: {UserId}",
                    paymentIntent.Id, request.UserId);
                
                // Don't create payment entity yet - wait for webhook confirmation
                return new ProcessPaymentResult(false, paymentIntent.ClientSecret,
                    $"Payment requires authentication. Status: {paymentIntent.Status}");
            }
            
            if (paymentIntent.Status != "succeeded")
            {
                _logger.LogWarning(
                    "Payment intent created but not succeeded. Status: {Status}, UserId: {UserId}",
                    paymentIntent.Status, request.UserId);
                return new ProcessPaymentResult(false, null, $"Payment not completed. Status: {paymentIntent.Status}");
            }

            // Create payment entity
            var payment = new Payment
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Amount = request.Amount,
                StripeId = paymentIntent.Id,
                Status = PaymentStatus.Paid,
                CreatedAtUtc = DateTime.UtcNow,
                SettledAtUtc = DateTime.UtcNow
            };

            // Update user balance
            user.Balance += request.Amount;

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Payment processed successfully. UserId: {UserId}, Amount: {Amount}, PaymentId: {PaymentId}, StripeId: {StripeId}",
                request.UserId, request.Amount, payment.Id, paymentIntent.Id);

            return new ProcessPaymentResult(true, payment.Id.ToString(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for user {UserId}", request.UserId);
            return new ProcessPaymentResult(false, null, $"Payment processing failed: {ex.Message}");
        }
    }
}