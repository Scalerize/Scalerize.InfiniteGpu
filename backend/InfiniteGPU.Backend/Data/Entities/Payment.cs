using System.ComponentModel.DataAnnotations.Schema;

namespace InfiniteGPU.Backend.Data.Entities;

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Refunded = 2,
    Failed = 3
}

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string UserId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string StripeId { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? SettledAtUtc { get; set; }

    // Navigation property
    public ApplicationUser? User { get; set; }
}
