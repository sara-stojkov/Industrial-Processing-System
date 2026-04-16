using IndustrialProcessingSystem.Config;
using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Processing;

public class JobProcessor
{
    private readonly Random _random = new();

    public int GetRequiredThreads(Job job)
    {
        if (job.Type == JobType.Prime)
        {
            var (_, threadCount) = PayloadParser.ParsePrime(job.Payload);
            return threadCount;
        }
        return 1; // IO jobs use 1 thread
    }

    public Task<int> ExecuteAsync(Job job)
    {
        return job.Type switch
        {
            JobType.Prime => ExecutePrimeAsync(job),
            JobType.IO => ExecuteIOAsync(job),
            _ => throw new ArgumentException($"Unknown job type: {job.Type}")
        };
    }

    private Task<int> ExecutePrimeAsync(Job job)
    {
        var (limit, threadCount) = PayloadParser.ParsePrime(job.Payload);

        return Task.Run(() =>
        {
            int count = 0;
            object countLock = new();

            var options = new ParallelOptions { MaxDegreeOfParallelism = threadCount };

            Parallel.For(2, limit + 1, options, i =>
            {
                if (IsPrime(i))
                    Interlocked.Increment(ref count);
            });

            return count;
        });
    }

    private Task<int> ExecuteIOAsync(Job job)
    {
        int delay = PayloadParser.ParseIO(job.Payload);

        return Task.Run(() =>
        {
            Thread.Sleep(delay);
            return _random.Next(0, 101);
        });
    }

    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        if (n == 2) return true;
        if (n % 2 == 0) return false;

        for (int i = 3; i * i <= n; i += 2)
            if (n % i == 0) return false;

        return true;
    }
}