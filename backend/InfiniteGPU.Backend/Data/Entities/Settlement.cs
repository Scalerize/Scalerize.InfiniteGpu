using System.ComponentModel.DataAnnotations.Schema;

namespace InfiniteGPU.Backend.Data.Entities;

public enum SettlementStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3
}

public class Settlement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public SettlementStatus Status { get; set; } = SettlementStatus.Pending;

    public string BankAccountDetails { get; set; } = string.Empty;

    public string? StripeTransferId { get; set; }

    public string? FailureReason { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;
}