using System.ComponentModel;

namespace InfiniteGPU.Backend.Shared.Models;

public enum TaskStatus
{
    [Description("Pending")]
    Pending = 0,

    [Description("Assigned")]
    Assigned = 1,

    [Description("InProgress")]
    InProgress = 2,

    [Description("Completed")]
    Completed = 3,

    [Description("Failed")]
    Failed = 4
}