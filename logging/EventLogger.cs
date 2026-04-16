namespace IndustrialProcessingSystem.Logging;

public class EventLogger
{
    private readonly string _logPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public EventLogger(string logPath = "events.log")
    {
        _logPath = logPath;
    }

    public async Task LogAsync(Guid jobId, string status, string result)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{status}] {jobId}, {result}";

        await _fileLock.WaitAsync();
        try
        {
            await File.AppendAllTextAsync(_logPath, line + Environment.NewLine);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}