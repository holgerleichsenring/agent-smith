namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// Settings for the in-flight run-artifact store (Redis in production, in-memory
/// in dev/tests). The TTL is the safety net for runs that crash without explicitly
/// promoting + clearing — past this window, abandoned runs disappear automatically.
/// </summary>
public sealed class PipelineStorageConfig
{
    public int RedisTtlHours { get; set; } = 4;
}
