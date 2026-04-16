using System.Collections.Concurrent;
using IndustrialProcessingSystem.Config;
using IndustrialProcessingSystem.Models;
using IndustrialProcessingSystem.Logging;
using IndustrialProcessingSystem.Reporting;

namespace IndustrialProcessingSystem.Processing;

public class ProcessingSystem
{
    private readonly PriorityJobQueue _queue;
    private readonly WorkerThreadPool _threadPool;
    private readonly JobProcessor _processor;
    private readonly EventLogger _logger;
    private readonly ReportManager _reportManager;
    private readonly CancellationTokenSource _cts = new();

    private readonly HashSet<Guid> _seenIds = new();
    private readonly object _seenLock = new();

    private readonly ConcurrentDictionary<Guid, Job> _allJobs = new();

    public event Func<Job, int, Task>? JobCompleted;
    public event Func<Job, Exception, Task>? JobFailed;

    public ProcessingSystem(SystemConfig config, EventLogger logger, ReportManager reportManager)
    {
        _queue = new PriorityJobQueue(config.MaxQueueSize);
        _threadPool = new WorkerThreadPool(config.WorkerCount);
        _processor = new JobProcessor();
        _logger = logger;
        _reportManager = reportManager;

        // Subscribe to events with lambdas as the spec requires
        JobCompleted += async (job, result) =>
            await _logger.LogAsync(job.Id, "COMPLETED", result.ToString());

        JobFailed += async (job, ex) =>
            await _logger.LogAsync(job.Id, "FAILED", ex.Message);

        // Start worker tasks
        for (int i = 0; i < config.WorkerCount; i++)
            Task.Run(() => WorkerLoop(_cts.Token));

        // Load initial jobs from config
        foreach (var job in config.InitialJobs)
            Submit(job);
    }

    public JobHandle Submit(Job job)
    {
        var handle = new JobHandle(job.Id);

        lock (_seenLock)
        {
            if (_seenIds.Contains(job.Id))
            {
                handle.Fail(new InvalidOperationException("Job already submitted."));
                return handle;
            }
            _seenIds.Add(job.Id);
        }

        _allJobs[job.Id] = job;

        bool accepted = _queue.TryEnqueue(job, handle);
        if (!accepted)
            handle.Fail(new InvalidOperationException("Queue is full. Job rejected."));

        return handle;
    }

    public Job? GetJob(Guid id)
    {
        _allJobs.TryGetValue(id, out var job);
        return job;
    }

    public IEnumerable<Job> GetTopJobs(int n)
    {
        return _queue.PeekTopN(n);
    }

    private async Task WorkerLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var item = await _queue.DequeueAsync(ct);
                if (item is null) continue;

                var (job, handle) = item.Value;
                await ExecuteWithRetryAsync(job, handle);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker error: {ex.Message}");
            }
        }
    }

    private async Task ExecuteWithRetryAsync(Job job, JobHandle handle)
    {
        const int maxAttempts = 3;
        var timeout = TimeSpan.FromSeconds(2);
        int requiredThreads = _processor.GetRequiredThreads(job);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var startTime = DateTime.Now;

            try
            {
                // Try to acquire threads within the 2s timeout window
                bool acquired = await _threadPool.TryAcquireAsync(requiredThreads, timeout);

                if (!acquired)
                {
                    if (attempt == maxAttempts)
                    {
                        await _logger.LogAsync(job.Id, "ABORT", "Could not acquire threads after 3 attempts.");
                        handle.Fail(new TimeoutException("Job aborted after 3 failed attempts."));
                        _reportManager.Record(job, DateTime.Now - startTime, success: false);
                        return;
                    }

                    if (JobFailed != null) await JobFailed(job, new TimeoutException("Timeout acquiring threads."));
                    continue;
                }

                try
                {
                    // Run the job and race it against the remaining timeout
                    var executionTask = _processor.ExecuteAsync(job);
                    var timeoutTask = Task.Delay(timeout);
                    var winner = await Task.WhenAny(executionTask, timeoutTask);

                    if (winner == timeoutTask)
                        throw new TimeoutException("Job execution exceeded 2 seconds.");

                    int result = await executionTask;
                    var duration = DateTime.Now - startTime;

                    handle.Complete(result);
                    _reportManager.Record(job, duration, success: true);

                    if (JobCompleted != null) await JobCompleted(job, result);
                    return;
                }
                finally
                {
                    _threadPool.Release(requiredThreads);
                }
            }
            catch (TimeoutException ex)
            {
                if (attempt == maxAttempts)
                {
                    await _logger.LogAsync(job.Id, "ABORT", ex.Message);
                    handle.Fail(ex);
                    _reportManager.Record(job, DateTime.Now - startTime, success: false);
                    return;
                }

                if (JobFailed != null) await JobFailed(job, ex);
            }
        }
    }

    public void Stop() => _cts.Cancel();
}