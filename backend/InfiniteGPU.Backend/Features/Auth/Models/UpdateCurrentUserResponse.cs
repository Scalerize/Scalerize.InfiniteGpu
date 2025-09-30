namespace InfiniteGPU.Backend.Features.Auth.Models;

public sealed record UpdateCurrentUserResponse(
    string UserId,
    string? FirstName,
    string? LastName,
    string? Email);