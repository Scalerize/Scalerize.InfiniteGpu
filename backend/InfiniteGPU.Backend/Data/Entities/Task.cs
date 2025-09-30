using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using InfiniteGPU.Backend.Shared.Models;
using TaskStatusEnum = InfiniteGPU.Backend.Shared.Models.TaskStatus;

namespace InfiniteGPU.Backend.Data.Entities;

public class Task
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Stored as integer representation of <see cref="TaskType" />.
    /// </summary>
    public TaskType Type { get; set; }

    /// <summary>
    /// Persistent storage URI for the uploaded ONNX model (blob or local path).
    /// </summary>
    [MaxLength(2048)]
    public string? OnnxModelBlobUri { get; set; }
    
    /// <summary>
    /// Stored as integer representation of <see cref="TaskStatus" />.
    /// </summary>
    public TaskStatusEnum Status { get; set; } = TaskStatusEnum.Pending;

    public bool FillBindingsViaApi { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedCost { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? LastProgressAtUtc { get; set; }

    public DateTime? LastHeartbeatAtUtc { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal CompletionPercent { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    [ForeignKey("UserId")]
    public virtual ApplicationUser User { get; set; } = null!;

    public virtual ICollection<Subtask> Subtasks { get; set; } = new List<Subtask>();

    public virtual ICollection<Withdrawal> Withdrawals { get; set; } = new List<Withdrawal>();

    public virtual ICollection<TaskInferenceBinding> InferenceBindings { get; set; } = new List<TaskInferenceBinding>();

    public virtual ICollection<TaskOutputBinding> OutputBindings { get; set; } = new List<TaskOutputBinding>();
}

[Owned]
public class TaskInferenceBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)]
    public string TensorName { get; set; } = string.Empty;
 
    public InferencePayloadType PayloadType { get; set; } = InferencePayloadType.Json;

    public string? Payload { get; set; }

    [MaxLength(2048)]
    public string? FileUrl { get; set; }
}

[Owned]
public class TaskOutputBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(256)]
    public string TensorName { get; set; } = string.Empty;

    public InferencePayloadType PayloadType { get; set; } = InferencePayloadType.Json;

    [MaxLength(50)]
    public string? FileFormat { get; set; }
}
