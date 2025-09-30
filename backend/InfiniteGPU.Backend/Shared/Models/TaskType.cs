using System.ComponentModel;

namespace InfiniteGPU.Backend.Shared.Models;

public enum TaskType
{
    [Description("Train")]
    Train = 0,

    [Description("Inference")]
    Inference = 1
}