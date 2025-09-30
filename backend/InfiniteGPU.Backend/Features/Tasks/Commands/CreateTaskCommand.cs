using System;
using MediatR;
using InfiniteGPU.Backend.Shared.Models;

namespace InfiniteGPU.Backend.Features.Tasks.Commands;

public record CreateTaskCommand(
    string UserId,
    Guid TaskId,
    string ModelUrl,
    TaskType Type,
    bool FillBindingsViaApi,
    Guid? InitialSubtaskId,
    CreateTaskCommand.InferenceParameters? Inference
) : IRequest<TaskDto>
{
    public record TrainParameters(int Epochs, int BatchSize);

    public record InferenceParameters
    {
        public IReadOnlyList<InferenceBinding> Bindings { get; init; } = Array.Empty<InferenceBinding>();

        public IReadOnlyList<OutputBinding> Outputs { get; init; } = Array.Empty<OutputBinding>();

        public record InferenceBinding(
            string TensorName,
            InferencePayloadType PayloadType,
            string? Payload,
            string? FileUrl);

        public record OutputBinding(
            string TensorName,
            InferencePayloadType PayloadType,
            string? FileFormat);
    }
}
