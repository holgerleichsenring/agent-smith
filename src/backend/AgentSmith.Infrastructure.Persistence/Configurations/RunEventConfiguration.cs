using AgentSmith.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// p0388a: the raw trail's per-step query index. One step's detail page is
/// served by (RunId, StepIndex) with Seq trailing so the page is both filtered
/// and ordered by the index — the read stays O(page) no matter how long the run
/// grew. The plain RunId index stays (whole-trail replay still uses it).
/// </summary>
public sealed class RunEventConfiguration : IEntityTypeConfiguration<RunEvent>
{
    public void Configure(EntityTypeBuilder<RunEvent> builder) =>
        builder.HasIndex(e => new { e.RunId, e.StepIndex, e.Seq });
}
