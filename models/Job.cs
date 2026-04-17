namespace IndustrialProcessingSystem.Models;

public record Job
{
    public Guid Id { get; init; }
    public JobType Type { get; init; }
    public string Payload { get; init; } = string.Empty;
    public int Priority { get; init; }
}