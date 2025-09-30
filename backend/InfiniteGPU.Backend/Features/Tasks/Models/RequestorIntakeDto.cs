namespace InfiniteGPU.Backend.Features.Tasks.Models;

public class RequestorIntakeDto
{
    public int ConnectedNodes { get; set; }
    public int TasksPerHour { get; set; }
    public int TotalProvidedTasks { get; set; }
    public int AvailableTasks { get; set; }
    public string TotalEarnings { get; set; } = string.Empty;
    public string TaskThroughput { get; set; } = string.Empty;
}