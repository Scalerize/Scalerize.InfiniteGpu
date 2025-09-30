using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InfiniteGPU.Backend.Data.Entities;

public class SubtaskTimelineEvent
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SubtaskId { get; set; }

    [MaxLength(128)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(2048)]
    public string? Message { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? MetadataJson { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SubtaskId))]
    public virtual Subtask Subtask { get; set; } = null!;
}