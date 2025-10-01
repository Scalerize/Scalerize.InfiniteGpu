namespace InfiniteGPU.Backend.Shared.Options;

public sealed class FrontendOptions
{
    public string PasswordResetUrl { get; set; } = string.Empty;
    public string BaseUrl { get;} = string.Empty;
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();
}