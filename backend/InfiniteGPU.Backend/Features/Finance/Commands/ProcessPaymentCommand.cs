namespace InfiniteGPU.Backend.Features.Finance.Commands;

public sealed record ProcessPaymentCommand(
    string UserId,
    decimal Amount,
    string StripePaymentMethodId) : MediatR.IRequest<ProcessPaymentResult>;

public sealed record ProcessPaymentResult(
    bool Success,
    string? PaymentId,
    string? ErrorMessage);