namespace IndustrialProcessingSystem.Processing;

public class WorkerThreadPool
{
    private readonly SemaphoreSlim _slots;
    private readonly int _totalThreads;

    public WorkerThreadPool(int totalThreads)
    {
        _totalThreads = totalThreads;
        _slots = new SemaphoreSlim(totalThreads, totalThreads);
    }

    public int Available => _slots.CurrentCount;
    public int Total => _totalThreads;

    public async Task<bool> TryAcquireAsync(int count, TimeSpan timeout)
    {
        // We need `count` slots simultaneously — acquire them one by one
        // but release all if we can't get them all within the timeout
        var acquired = new List<bool>();

        try
        {
            for (int i = 0; i < count; i++)
            {
                bool got = await _slots.WaitAsync(timeout);
                if (!got)
                {
                    // Release what we already acquired and give up
                    for (int j = 0; j < acquired.Count; j++)
                        _slots.Release();
                    return false;
                }
                acquired.Add(true);
            }
            return true;
        }
        catch
        {
            for (int j = 0; j < acquired.Count; j++)
                _slots.Release();
            return false;
        }
    }

    public void Release(int count)
    {
        _slots.Release(count);
    }
}