using System.ComponentModel.DataAnnotations;

namespace InfiniteGPU.Backend.Shared.Options;

public sealed class AzureStorageOptions
{
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    public string ModelsContainerName { get; set; } = "models";

    [Required]
    public string InferenceContainerName { get; set; } = "inference";

    [Required]
    public string TrainingContainerName { get; set; } = "training";

    /// <summary>
    /// Time-to-live for generated upload URLs, expressed in minutes.
    /// </summary>
    [Range(1, 1440)]
    public int UploadUrlTtlMinutes { get; set; } = 15;
}