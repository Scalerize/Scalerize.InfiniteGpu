using System.Text.Json;
using System.Text.Json.Serialization;
using InfiniteGPU.Backend.Data.Entities;
using InfiniteGPU.Backend.Shared.Models;

namespace InfiniteGPU.Backend.Features.Subtasks;

internal static class SubtaskMapping
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static SubtaskDto CreateDto(Subtask subtask, bool isRequestorView = false)
    {
        var task = subtask.Task ?? throw new InvalidOperationException("Subtask.Task must be included");

        return new SubtaskDto
        {
            Id = subtask.Id,
            TaskId = task.Id,
            TaskType = task.Type,
            Status = subtask.Status,
            Progress = subtask.Progress,
            ParametersJson = subtask.Params,
            AssignedProviderId = subtask.AssignedProviderId,
            DeviceId = subtask.DeviceId,
            ExecutionState = ParseExecutionState(subtask.ExecutionStateJson),
            EstimatedEarnings = subtask.CostUsd ?? 0,
            DurationSeconds = subtask.DurationSeconds,
            CostUsd = isRequestorView ? subtask.CostUsd * 1.2m : subtask.CostUsd,
            CreatedAtUtc = subtask.CreatedAt,
            AssignedAtUtc = subtask.AssignedAt,
            StartedAtUtc = subtask.StartedAt,
            CompletedAtUtc = subtask.CompletedAt,
            FailedAtUtc = subtask.FailedAtUtc,
            FailureReason = subtask.FailureReason,
            LastHeartbeatAtUtc = subtask.LastHeartbeatAtUtc,
            NextHeartbeatDueAtUtc = subtask.NextHeartbeatDueAtUtc,
            LastCommandAtUtc = subtask.LastCommandAtUtc,
            RequiresReassignment = subtask.RequiresReassignment,
            ReassignmentRequestedAtUtc = subtask.ReassignmentRequestedAtUtc,
            OnnxModel = BuildOnnxModelMetadata(subtask, task),
            Timeline = BuildTimeline(subtask),
            ConcurrencyToken = EncodeRowVersion(subtask.RowVersion),
            InputArtifacts = BuildInputArtifacts(task),
            OutputArtifacts = ParseOutputArtifacts(subtask.ResultsJson)
        };
    }

    private static OnnxModelMetadataDto BuildOnnxModelMetadata(Subtask subtask, Data.Entities.Task task)
    {
        return new OnnxModelMetadataDto
        {
            BlobUri = subtask.OnnxModelBlobUri
        };
    }

    private static IReadOnlyList<SubtaskTimelineEventDto> BuildTimeline(Subtask subtask)
    {
        if (subtask.TimelineEvents is null || subtask.TimelineEvents.Count == 0)
        {
            return Array.Empty<SubtaskTimelineEventDto>();
        }

        return subtask.TimelineEvents
            .OrderByDescending(e => e.CreatedAtUtc)
            .Select(e => new SubtaskTimelineEventDto
            {
                Id = e.Id,
                EventType = e.EventType,
                Message = e.Message,
                MetadataJson = e.MetadataJson,
                CreatedAtUtc = e.CreatedAtUtc
            })
            .ToArray();
    }

    private static SubtaskDto.ExecutionStateDto? ParseExecutionState(string? executionStateJson)
    {
        if (string.IsNullOrWhiteSpace(executionStateJson))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<ExecutionStateModel>(executionStateJson, JsonOptions);
            if (state is null)
            {
                return null;
            }

            return new SubtaskDto.ExecutionStateDto
            {
                Phase = state.Phase ?? "pending",
                Message = state.Message,
                ProviderUserId = state.ProviderUserId,
                OnnxModelReady = state.OnnxModelReady,
                WebGpuPreferred = state.WebGpuPreferred,
                ExtendedMetadata = state.ExtendedMetadata
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<SubtaskDto.InputArtifactDto> BuildInputArtifacts(Data.Entities.Task task)
    {
        if (task.InferenceBindings == null || task.InferenceBindings.Count == 0)
        {
            return Array.Empty<SubtaskDto.InputArtifactDto>();
        }

        return task.InferenceBindings
            .Where(b => !string.IsNullOrWhiteSpace(b.FileUrl) || !string.IsNullOrWhiteSpace(b.Payload))
            .Select(binding => new SubtaskDto.InputArtifactDto
            {
                TensorName = binding.TensorName,
                PayloadType = binding.PayloadType.ToString(),
                FileUrl = binding.FileUrl,
                Payload = string.IsNullOrWhiteSpace(binding.FileUrl) ? binding.Payload : null
            })
            .ToArray();
    }

    private static IReadOnlyList<SubtaskDto.OutputArtifactDto> ParseOutputArtifacts(string? resultsJson)
    {
        if (string.IsNullOrWhiteSpace(resultsJson))
        {
            return Array.Empty<SubtaskDto.OutputArtifactDto>();
        }

        try
        {
            var result = JsonSerializer.Deserialize<SubtaskResultModel>(resultsJson, JsonOptions);
            if (result?.Outputs == null || result.Outputs.Count == 0)
            {
                return Array.Empty<SubtaskDto.OutputArtifactDto>();
            }

            return result.Outputs
                .Where(o => !string.IsNullOrWhiteSpace(o.FileUrl) || !string.IsNullOrWhiteSpace(o.Payload))
                .Select(output => new SubtaskDto.OutputArtifactDto
                {
                    TensorName = output.TensorName ?? string.Empty,
                    FileUrl = output.FileUrl,
                    FileFormat = output.Format,
                    PayloadType = output.PayloadType,
                    Payload = string.IsNullOrWhiteSpace(output.FileUrl) ? output.Payload : null
                })
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<SubtaskDto.OutputArtifactDto>();
        }
    }

    private static string EncodeRowVersion(byte[]? rowVersion) =>
        rowVersion is { Length: > 0 } ? Convert.ToBase64String(rowVersion) : string.Empty;

    private sealed class ExecutionSpecModel
    {
        public string? RunMode { get; set; }

        public string? OnnxModelUrl { get; set; }

        public int[]? InputTensorShape { get; set; }

        public ShardDescriptorModel? Shard { get; set; }


        public sealed class ShardDescriptorModel
        {
            public int Index { get; set; }

            public int Count { get; set; }

            public decimal Fraction { get; set; }
        }
    }

    private sealed class ExecutionStateModel
    {
        public string? Phase { get; set; }
        public string? Message { get; set; }
        public string? ProviderUserId { get; set; }
        public bool? OnnxModelReady { get; set; }
        public bool? WebGpuPreferred { get; set; }
        public IDictionary<string, object?>? ExtendedMetadata { get; set; }
    }

    private sealed class AssignmentHistoryEntryModel
    {
        public string? ProviderUserId { get; set; }
        public DateTime AssignedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? FailedAtUtc { get; set; }
        public DateTime? LastHeartbeatAtUtc { get; set; }
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class SubtaskResultModel
    {
        public Guid SubtaskId { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public ResultMetricsModel? Metrics { get; set; }
        public List<OutputArtifactModel>? Outputs { get; set; }
    }

    private sealed class ResultMetricsModel
    {
        public double DurationSeconds { get; set; }
        public decimal CostUsd { get; set; }
        public string? Device { get; set; }
    }

    private sealed class OutputArtifactModel
    {
        public string? TensorName { get; set; }
        public string? FileUrl { get; set; }
        public string? Format { get; set; }
        public string? Payload { get; set; }

        public InferencePayloadType PayloadType { get; set; }
    }
}
