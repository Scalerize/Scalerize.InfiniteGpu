using System.ComponentModel.DataAnnotations.Schema;

namespace InfiniteGPU.Backend.Data.Entities;

public enum EarningStatus
{
    Pending = 0,
    Paid = 1,
    Failed = 2
}

public class Earning
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string ProviderUserId { get; set; } = string.Empty;

    public Guid TaskId { get; set; }

    public Guid SubtaskId { get; set; }

    public decimal Amount { get; set; }

    public EarningStatus Status { get; set; } = EarningStatus.Pending;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    [ForeignKey("ProviderUserId")]
    public virtual ApplicationUser Provider { get; set; } = null!;

    [ForeignKey("TaskId")]
    public virtual Task Task { get; set; } = null!;

    [ForeignKey("SubtaskId")]
    public virtual Subtask Subtask { get; set; } = null!;
}