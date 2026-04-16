using System.Collections.Concurrent;
using System.Xml.Linq;
using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Reporting;

public class ReportManager
{
    private readonly ConcurrentBag<JobResult> _results = new();
    private readonly string _reportDirectory;
    private int _reportIndex = 0;
    private readonly object _indexLock = new();

    public ReportManager(string reportDirectory = "reports")
    {
        _reportDirectory = reportDirectory;
        Directory.CreateDirectory(reportDirectory);

        // Timer fires every 60 seconds
        var timer = new System.Timers.Timer(60_000);
        timer.Elapsed += (_, _) => GenerateReport();
        timer.Start();
    }

    public void Record(Job job, TimeSpan duration, bool success)
    {
        _results.Add(new JobResult
        {
            JobId = job.Id,
            Type = job.Type,
            Success = success,
            Duration = duration,
            CompletedAt = DateTime.Now
        });
    }

    private void GenerateReport()
    {
        try
        {
            var snapshot = _results.ToList();

            var byType = snapshot
                .GroupBy(r => r.Type)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count(r => r.Success),
                    AvgDuration = g.Where(r => r.Success)
                                   .Select(r => r.Duration.TotalMilliseconds)
                                   .DefaultIfEmpty(0)
                                   .Average(),
                    FailCount = g.Count(r => !r.Success)
                })
                .OrderBy(x => x.Type.ToString())
                .ToList();

            var doc = new XDocument(
                new XElement("Report",
                    new XAttribute("GeneratedAt", DateTime.Now.ToString("o")),
                    byType.Select(t =>
                        new XElement("JobType",
                            new XAttribute("Type", t.Type),
                            new XElement("Completed", t.Count),
                            new XElement("AvgDurationMs", Math.Round(t.AvgDuration, 2)),
                            new XElement("Failed", t.FailCount)
                        )
                    )
                )
            );

            int index;
            lock (_indexLock)
            {
                index = _reportIndex % 10;
                _reportIndex++;
            }

            string path = Path.Combine(_reportDirectory, $"report_{index}.xml");
            doc.Save(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Report generation failed: {ex.Message}");
        }
    }
}