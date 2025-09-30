using System.ComponentModel.DataAnnotations.Schema;

namespace InfiniteGPU.Backend.Data.Entities;

public enum WithdrawalStatus
{
    Pending = 0,
    Settled = 1,
    Refunded = 2,
    Failed = 3
}

public class Withdrawal
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RequestorUserId { get; set; } = string.Empty;

    public Guid TaskId { get; set; }

    public Guid SubtaskId { get; set; }

    public decimal Amount { get; set; }

    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? SettledAtUtc { get; set; }

    [ForeignKey(nameof(RequestorUserId))]
    public virtual ApplicationUser Requestor { get; set; } = null!;

    [ForeignKey(nameof(TaskId))]
    public virtual Task Task { get; set; } = null!;

    [ForeignKey(nameof(SubtaskId))]
    public virtual Subtask Subtask { get; set; } = null!;
}