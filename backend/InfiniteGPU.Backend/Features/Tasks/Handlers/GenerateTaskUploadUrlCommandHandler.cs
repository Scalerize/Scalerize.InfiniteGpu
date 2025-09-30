using InfiniteGPU.Backend.Features.Tasks.Commands;
using InfiniteGPU.Backend.Shared.Services;
using MediatR;

namespace InfiniteGPU.Backend.Features.Tasks.Handlers;

public sealed class GenerateTaskUploadUrlCommandHandler : IRequestHandler<GenerateTaskUploadUrlCommand, TaskUploadUrlResult>
{
    private readonly ITaskUploadUrlService _taskUploadUrlService;

    public GenerateTaskUploadUrlCommandHandler(ITaskUploadUrlService taskUploadUrlService)
    {
        _taskUploadUrlService = taskUploadUrlService;
    }

    public Task<TaskUploadUrlResult> Handle(GenerateTaskUploadUrlCommand request, CancellationToken cancellationToken)
    {
        return _taskUploadUrlService.GenerateUploadUrlAsync(
            request.UserId,
            request.TaskId,
            request.SubtaskId,
            request.FileType,
            request.InputName,
            request.FileExtension,
            cancellationToken);
    }
}