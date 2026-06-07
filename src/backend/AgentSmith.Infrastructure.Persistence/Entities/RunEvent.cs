namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// One event from the run's typed event trail. Seq orders events within a run.
/// PayloadJson is the serialized typed event. High-volume + per-event payloads:
/// inserts are batched and pruned by the retention policy (p0246c).
/// </summary>
public sealed class RunEvent : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public long Seq { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string? Role { get; set; }
    public string? Phase { get; set; }
    public string? Repo { get; set; }
    public string? PayloadJson { get; set; }
}
