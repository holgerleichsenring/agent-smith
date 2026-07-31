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

    /// <summary>
    /// p0388a: the pipeline step this event was produced in, taken from the
    /// event's own producer stamp. Indexed with (RunId, Seq) so one step's page
    /// is a bounded query instead of a scan of the run's whole trail. Null on
    /// rows written before p0388a and on events raised outside any step.
    /// </summary>
    public int? StepIndex { get; set; }
}
