using System.ComponentModel.DataAnnotations;

namespace InfiniteGPU.Backend.Features.Subtasks.Models;

/// <summary>
/// Provider payload to mark an assigned subtask as failed.
/// </summary>
public sealed class SubtaskFailureRequest
{
    /// <summary>
    /// Optimistic concurrency token derived from the subtask row version.
    /// </summary>
    [MaxLength(512)]
    public string? ConcurrencyToken { get; init; }

    /// <summary>
    /// Required reason describing the failure condition.
    /// </summary>
    [Required]
    [MaxLength(2048)]
    public string FailureReason { get; init; } = string.Empty;

    /// <summary>
    /// Indicates whether the subtask should be returned to the queue for reassignment.
    /// </summary>
    public bool RequiresReassignment { get; init; }

    /// <summary>
    /// Optional structured metadata describing failure diagnostics.
    /// </summary>
    [MaxLength(4096)]
    public string? FailureMetadataJson { get; init; }
}