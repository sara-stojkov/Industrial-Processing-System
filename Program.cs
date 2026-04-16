using IndustrialProcessingSystem.Config;
using IndustrialProcessingSystem.Logging;
using IndustrialProcessingSystem.Models;
using IndustrialProcessingSystem.Processing;
using IndustrialProcessingSystem.Reporting;

var config = ConfigLoader.Load("SystemConfig.xml");
var logger = new EventLogger("events.log");
var reporter = new ReportManager("reports");
var system = new ProcessingSystem(config, logger, reporter);

var random = new Random();
var jobTypes = new[] { JobType.Prime, JobType.IO };

var producerThreads = Enumerable.Range(0, config.ProducerCount).Select(_ => new Thread(() =>
{
    while (true)
    {
        try
        {
            var type = jobTypes[random.Next(jobTypes.Length)];

            string payload = type == JobType.Prime
                ? $"numbers:{random.Next(5000, 50000)},threads:{random.Next(1, 9)}"
                : $"delay:{random.Next(500, 4000)}";

            var job = new Job
            {
                Id = Guid.NewGuid(),
                Type = type,
                Payload = payload,
                Priority = random.Next(1, 6)
            };

            system.Submit(job);
            Thread.Sleep(random.Next(200, 1000));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Producer error: {ex.Message}");
        }
    }
})).ToList();

producerThreads.ForEach(t => { t.IsBackground = true; t.Start(); });

Console.WriteLine("System running. Press Enter to stop.");
Console.ReadLine();
system.Stop();