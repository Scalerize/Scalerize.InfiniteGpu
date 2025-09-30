using System.Security.Claims;
using InfiniteGPU.Backend.Features.Finance.Commands;
using InfiniteGPU.Backend.Features.Finance.Queries;
using MediatR;

namespace InfiniteGPU.Backend.Features.Finance.Endpoints;

public static class FinanceEndpoints
{
    public static void MapFinanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/finance")
            .WithTags("Finance")
            .RequireAuthorization();

        group.MapGet("/summary", GetFinanceSummaryAsync)
            .WithName("GetFinanceSummary")
            .WithOpenApi()
            .RequireAuthorization();

        group.MapPost("/topup", ProcessTopUpAsync)
            .WithName("ProcessTopUp")
            .WithOpenApi()
            .RequireAuthorization();

        group.MapPost("/settlement", CreateSettlementAsync)
            .WithName("CreateSettlement")
            .WithOpenApi()
            .RequireAuthorization();
    }

    private static async Task<IResult> GetFinanceSummaryAsync(
        ClaimsPrincipal principal,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var summary = await mediator.Send(new GetFinanceSummaryQuery(userId), cancellationToken);
        return Results.Ok(summary);
    }

    private static async Task<IResult> ProcessTopUpAsync(
        ClaimsPrincipal principal,
        TopUpRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var command = new ProcessPaymentCommand(userId, request.Amount, request.StripePaymentMethodId);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Ok(new { paymentId = result.PaymentId });
    }

    private static async Task<IResult> CreateSettlementAsync(
        ClaimsPrincipal principal,
        SettlementRequest request,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        var command = new CreateSettlementCommand(userId, request.Amount, request.Country, request.BankAccountDetails);
        var result = await mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return Results.BadRequest(new { error = result.ErrorMessage });
        }

        return Results.Ok(new { settlementId = result.SettlementId });
    }
}

public sealed record TopUpRequest(decimal Amount, string StripePaymentMethodId);
public sealed record SettlementRequest(decimal Amount, string Country, string BankAccountDetails);