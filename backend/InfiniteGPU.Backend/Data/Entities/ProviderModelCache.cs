using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace InfiniteGPU.Backend.Data.Entities;

public sealed class ProviderModelCache
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(450)]
    public string ProviderUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string StoredFileName { get; set; } = string.Empty;

    public DateTime LastAccessedAtUtc { get; set; } = DateTime.UtcNow;

    public int AccessCount { get; set; }

    [ForeignKey(nameof(ProviderUserId))]
    public ApplicationUser Provider { get; set; } = null!;
}