using System.Xml.Linq;
using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Config;

public static class ConfigLoader
{
    public static SystemConfig Load(string path)
    {
        var doc = XDocument.Load(path);
        var root = doc.Root!;

        var config = new SystemConfig
        {
            WorkerCount = (int)root.Element("WorkerCount")!,
            MaxQueueSize = (int)root.Element("MaxQueueSize")!,
        };

        config.InitialJobs = root.Element("Jobs")?
            .Elements("Job")
            .Select(j => new Job
            {
                Id = Guid.NewGuid(),
                Type = Enum.Parse<JobType>(j.Attribute("Type")!.Value),
                Payload = j.Attribute("Payload")!.Value,
                Priority = int.Parse(j.Attribute("Priority")!.Value)
            })
            .ToList() ?? new();

        return config;
    }
}