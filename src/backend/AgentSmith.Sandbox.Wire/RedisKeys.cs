namespace AgentSmith.Sandbox.Wire;

public static class RedisKeys
{
    public const string Prefix = "sandbox";

    public static string InputKey(string jobId) => $"{Prefix}:{jobId}:in";
    public static string EventsKey(string jobId) => $"{Prefix}:{jobId}:events";
    public static string ResultsKey(string jobId) => $"{Prefix}:{jobId}:results";
}
