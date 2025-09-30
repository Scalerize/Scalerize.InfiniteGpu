using Stripe;
using Microsoft.EntityFrameworkCore;
using InfiniteGPU.Backend.Data;
using InfiniteGPU.Backend.Data.Entities;
using TaskEntity = InfiniteGPU.Backend.Data.Entities.Task;

namespace InfiniteGPU.Backend.Features.Finance.Endpoints;

public static class StripeWebhookEndpoints
{
    public static void MapStripeWebhookEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/webhooks/stripe", HandleStripeWebhook)
            .WithName("StripeWebhook")
            .WithOpenApi()
            .AllowAnonymous(); // Stripe webhooks are authenticated via signature
    }

    private static async Task<IResult> HandleStripeWebhook(
        HttpContext httpContext,
        IConfiguration configuration,
        AppDbContext context,
        ILogger<Program> logger)
    {
        var json = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
        
        try
        {
            var stripeSignatureHeader = httpContext.Request.Headers["Stripe-Signature"].FirstOrDefault();
            var webhookSecret = configuration["Stripe:WebhookSecret"];

            if (string.IsNullOrWhiteSpace(webhookSecret))
            {
                logger.LogError("Stripe webhook secret not configured");
                return Results.Problem("Webhook not configured", statusCode: 500);
            }

            if (string.IsNullOrWhiteSpace(stripeSignatureHeader))
            {
                logger.LogWarning("Stripe webhook received without signature");
                return Results.Unauthorized();
            }

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    stripeSignatureHeader,
                    webhookSecret
                );
            }
            catch (StripeException ex)
            {
                logger.LogError(ex, "Failed to verify Stripe webhook signature");
                return Results.Unauthorized();
            }

            logger.LogInformation("Stripe webhook received: {EventType}", stripeEvent.Type);

            // Handle different event types
            switch (stripeEvent.Type)
            {
                case "payout.paid":
                    await HandlePayoutPaid(stripeEvent, context, logger);
                    break;

                case "payout.failed":
                    await HandlePayoutFailed(stripeEvent, context, logger);
                    break;

                case "payout.canceled":
                    await HandlePayoutCanceled(stripeEvent, context, logger);
                    break;

                case "account.updated":
                    await HandleAccountUpdated(stripeEvent, context, logger);
                    break;

                default:
                    logger.LogInformation("Unhandled webhook event type: {EventType}", stripeEvent.Type);
                    break;
            }

            return Results.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing Stripe webhook");
            return Results.Problem("Error processing webhook", statusCode: 500);
        }
    }

    private static async System.Threading.Tasks.Task HandlePayoutPaid(Event stripeEvent, AppDbContext context, ILogger logger)
    {
        var payout = (Payout)stripeEvent.Data.Object;
        
        var settlement = await context.Settlements
            .FirstOrDefaultAsync(s => s.StripeTransferId == payout.Id);

        if (settlement == null)
        {
            logger.LogWarning("Settlement not found for payout: {PayoutId}", payout.Id);
            return;
        }

        settlement.Status = SettlementStatus.Completed;
        settlement.CompletedAtUtc = DateTime.UtcNow;
        settlement.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogInformation(
            "Settlement {SettlementId} marked as completed for payout {PayoutId}",
            settlement.Id, payout.Id);
    }

    private static async System.Threading.Tasks.Task HandlePayoutFailed(Event stripeEvent, AppDbContext context, ILogger logger)
    {
        var payout = (Payout)stripeEvent.Data.Object;
        
        var settlement = await context.Settlements
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StripeTransferId == payout.Id);

        if (settlement == null)
        {
            logger.LogWarning("Settlement not found for failed payout: {PayoutId}", payout.Id);
            return;
        }

        // Refund user balance if not already done
        if (settlement.Status != SettlementStatus.Failed)
        {
            settlement.User.Balance += settlement.Amount;
        }

        settlement.Status = SettlementStatus.Failed;
        settlement.FailureReason = payout.FailureMessage ?? "Payout failed";
        settlement.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogWarning(
            "Settlement {SettlementId} failed for payout {PayoutId}: {Reason}",
            settlement.Id, payout.Id, settlement.FailureReason);
    }

    private static async System.Threading.Tasks.Task HandlePayoutCanceled(Event stripeEvent, AppDbContext context, ILogger logger)
    {
        var payout = (Payout)stripeEvent.Data.Object;
        
        var settlement = await context.Settlements
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.StripeTransferId == payout.Id);

        if (settlement == null)
        {
            logger.LogWarning("Settlement not found for canceled payout: {PayoutId}", payout.Id);
            return;
        }

        // Refund user balance if not already done
        if (settlement.Status != SettlementStatus.Failed)
        {
            settlement.User.Balance += settlement.Amount;
        }

        settlement.Status = SettlementStatus.Failed;
        settlement.FailureReason = "Payout canceled";
        settlement.UpdatedAtUtc = DateTime.UtcNow;

        await context.SaveChangesAsync();

        logger.LogWarning(
            "Settlement {SettlementId} canceled for payout {PayoutId}",
            settlement.Id, payout.Id);
    }

    private static async System.Threading.Tasks.Task HandleAccountUpdated(Event stripeEvent, AppDbContext context, ILogger logger)
    {
        var account = (Account)stripeEvent.Data.Object;
        
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.StripeConnectedAccountId == account.Id);

        if (user == null)
        {
            logger.LogInformation("User not found for account update: {AccountId}", account.Id);
            return;
        }

        logger.LogInformation(
            "Account updated for user {UserId}: {AccountId}, ChargesEnabled: {ChargesEnabled}, PayoutsEnabled: {PayoutsEnabled}",
            user.Id, account.Id, account.ChargesEnabled, account.PayoutsEnabled);
    }
}