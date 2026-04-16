using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Config;

public class SystemConfig
{
    public int WorkerCount { get; set; }
    public int ProducerCount { get; set; } = 5; // not in XML, sensible default
    public int MaxQueueSize { get; set; }
    public List<Job> InitialJobs { get; set; } = new();
}