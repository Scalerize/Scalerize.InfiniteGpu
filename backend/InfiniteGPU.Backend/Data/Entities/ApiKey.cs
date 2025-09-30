using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InfiniteGPU.Backend.Data.Entities;

public class ApiKey
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Prefix { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    [MaxLength(128)]
    public string Key { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedAtUtc { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser User { get; set; } = null!;
}