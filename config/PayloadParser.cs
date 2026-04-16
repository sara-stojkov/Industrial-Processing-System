namespace IndustrialProcessingSystem.Config;

public static class PayloadParser
{
    // "numbers:10_000,threads:3" -> (100000, 3)
    public static (int limit, int threadCount) ParsePrime(string payload)
    {
        var parts = ParseParts(payload);
        int limit = int.Parse(parts["numbers"].Replace("_", ""));
        int threadCount = int.Parse(parts["threads"]);
        threadCount = Math.Clamp(threadCount, 1, 8);
        return (limit, threadCount);
    }

    // "delay:1_000" -> 1000S
    public static int ParseIO(string payload)
    {
        var parts = ParseParts(payload);
        return int.Parse(parts["delay"].Replace("_", ""));
    }

    private static Dictionary<string, string> ParseParts(string payload)
    {
        return payload
            .Split(',')
            .Select(p => p.Split(':'))
            .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
    }
}