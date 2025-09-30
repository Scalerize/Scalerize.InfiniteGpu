using System.ComponentModel.DataAnnotations;

namespace InfiniteGPU.Backend.Features.Subtasks.Models;

/// <summary>
/// Provider heartbeat payload to update liveness for an assigned subtask.
/// </summary>
public sealed class SubtaskHeartbeatRequest
{
    /// <summary>
    /// Optional concurrency token derived from the subtask row version to enforce optimistic concurrency.
    /// </summary>
    [MaxLength(512)]
    public string? ConcurrencyToken { get; init; }

    /// <summary>
    /// Optional provider-specified heartbeat grace interval. When omitted the backend applies a default window.
    /// </summary>
    public TimeSpan? HeartbeatGrace { get; init; }
}