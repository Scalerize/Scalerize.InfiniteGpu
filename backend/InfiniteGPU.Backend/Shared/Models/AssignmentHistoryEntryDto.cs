namespace InfiniteGPU.Backend.Shared.Models;

/// <summary>
/// Represents a single provider assignment lifecycle record for a subtask.
/// </summary>
public sealed class AssignmentHistoryEntryDto
{
    public string ProviderUserId { get; init; } = string.Empty;

    public DateTime AssignedAtUtc { get; init; }

    public DateTime? StartedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public DateTime? FailedAtUtc { get; init; }

    public DateTime? LastHeartbeatAtUtc { get; init; }

    public string Status { get; init; } = string.Empty;

    public string? Notes { get; init; }
}