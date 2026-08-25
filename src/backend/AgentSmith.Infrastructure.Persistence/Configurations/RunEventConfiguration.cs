using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0388a: the raw trail's per-step query index. One step's detail page is
/// served by (RunId, StepIndex) with Seq trailing so the page is both filtered
/// and ordered by the index — the read stays O(page) no matter how long the run
/// grew. The plain RunId index stays (whole-trail replay still uses it).
/// <para>
/// 2026-08-25-61f1: a run's trail position is also the trail's IDENTITY — one row per
/// (RunId, Seq), enforced. The buffer and the terminal reconciler both derive their next
/// sequence from what the store already holds, and a replay that re-mints a position the
/// store has is what turned one run's 1343 events into 56406 rows.
/// </para>
/// </summary>
public sealed class RunEventConfiguration : IEntityTypeConfiguration<RunEvent>
{
    public void Configure(EntityTypeBuilder<RunEvent> builder)
    {
        builder.HasIndex(e => new { e.RunId, e.StepIndex, e.Seq });
        builder.HasIndex(e => new { e.RunId, e.Seq }).IsUnique();
    }
}
