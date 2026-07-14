namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0336: a run's computed capacity footprint + its live reservation state — the
/// durable ledger row keyed by runId (one per run). FootprintJson is the
/// operator-facing breakdown (pods, dropped contexts, totals); TotalCpuNanos /
/// TotalMemBytes are the summable figures the budget gates on. Reserved=false
/// while a run is only queued (footprint visible, no budget held); flipped true
/// when it starts, so sum(reserved) &lt;= budget across running runs. Deleted on
/// terminal status or when the run is deleted (p0337 satellite).
/// </summary>
public sealed class RunCapacity : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;
    public string FootprintJson { get; set; } = string.Empty;
    public long TotalCpuNanos { get; set; }
    public long TotalMemBytes { get; set; }
    public bool Reserved { get; set; }
}
