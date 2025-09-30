using InfiniteGPU.Backend.Features.Inference.Handlers;
using InfiniteGPU.Backend.Features.Inference.Models;
using InfiniteGPU.Backend.Shared.Services;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace InfiniteGPU.Backend.Features.Inference.Endpoints;

public static class InferenceEndpoints
{
    public static void MapInferenceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inference")
            .WithTags("Inference");

        group.MapPost("/tasks/{taskId:guid}", SubmitInferenceAsync)
            .WithName("SubmitInference")
            .WithOpenApi();

        group.MapGet("/subtasks/{subtaskId:guid}", GetInferenceSubtaskAsync)
            .WithName("GetInferenceSubtask")
            .WithOpenApi();
    }

    private static async Task<IResult> SubmitInferenceAsync(
        Guid taskId,
        HttpContext httpContext,
        IMediator mediator,
        [FromBody] SubmitInferenceRequest request,
        CancellationToken cancellationToken)
    {
        var apiKeyValue = ApiKeyAuthenticationService.ReadApiKeyFromHeaders(httpContext.Request.Headers);
        if (string.IsNullOrWhiteSpace(apiKeyValue))
        {
            return Results.Unauthorized();
        }

        try
        {
            var response = await mediator.Send(
                new SubmitInferenceCommand(apiKeyValue, taskId, request),
                cancellationToken);

            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetInferenceSubtaskAsync(
        Guid subtaskId,
        HttpContext httpContext,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var apiKeyValue = ApiKeyAuthenticationService.ReadApiKeyFromHeaders(httpContext.Request.Headers);
        if (string.IsNullOrWhiteSpace(apiKeyValue))
        {
            return Results.Unauthorized();
        }

        try
        {
            var response = await mediator.Send(
                new GetInferenceSubtaskQuery(apiKeyValue, subtaskId),
                cancellationToken);

            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Unauthorized();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }
}