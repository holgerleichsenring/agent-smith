namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-20-24bd: one place a run holds on a model deployment. <see cref="Slot"/> is the place's index below the
/// agent's <c>max_concurrent_runs</c>; the unique (Key, Slot) index is what admits one run per place across replicas.
/// Deleted when the run stops executing, ends or is deleted.
/// </summary>
public sealed class ModelRunSlot : EntityBase
{
    public long Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public int Slot { get; set; }
    public string RunId { get; set; } = string.Empty;
    public DateTimeOffset AcquiredAt { get; set; }
}
