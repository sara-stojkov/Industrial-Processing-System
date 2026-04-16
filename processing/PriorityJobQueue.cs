using IndustrialProcessingSystem.Models;

namespace IndustrialProcessingSystem.Processing;

public class PriorityJobQueue
{
    private readonly PriorityQueue<(Job job, JobHandle handle), int> _queue = new();
    private readonly int _maxSize;
    private readonly object _lock = new();
    private readonly SemaphoreSlim _itemAvailable = new(0);

    public PriorityJobQueue(int maxSize)
    {
        _maxSize = maxSize;
    }

    public bool TryEnqueue(Job job, JobHandle handle)
    {
        lock (_lock)
        {
            if (_queue.Count >= _maxSize)
                return false;

            _queue.Enqueue((job, handle), job.Priority);
        }

        _itemAvailable.Release();
        return true;
    }

    public async Task<(Job job, JobHandle handle)?> DequeueAsync(CancellationToken ct)
    {
        await _itemAvailable.WaitAsync(ct);

        lock (_lock)
        {
            if (_queue.TryDequeue(out var item, out _))
                return item;
        }

        return null;
    }

    public IEnumerable<Job> PeekTopN(int n)
    {
        lock (_lock)
        {
            return _queue.UnorderedItems
                .OrderBy(x => x.Priority)
                .Take(n)
                .Select(x => x.Element.job)
                .ToList();
        }
    }

    public int Count
    {
        get { lock (_lock) return _queue.Count; }
    }
}