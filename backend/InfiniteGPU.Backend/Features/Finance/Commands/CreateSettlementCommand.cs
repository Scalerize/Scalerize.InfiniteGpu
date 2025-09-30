namespace InfiniteGPU.Backend.Features.Finance.Commands;

public sealed record CreateSettlementCommand(
    string UserId,
    decimal Amount,
    string Country,
    string BankAccountDetails) : MediatR.IRequest<CreateSettlementResult>;

public sealed record CreateSettlementResult(
    bool Success,
    string? SettlementId,
    string? ErrorMessage);