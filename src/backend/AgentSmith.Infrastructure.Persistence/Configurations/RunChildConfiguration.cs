using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Infrastructure.Persistence.Configurations;

/// <summary>
/// How a Run's children are related to it: by a plain indexed <c>RunId</c> column and NOT
/// by an enforced foreign key.
/// <para>
/// A child — an artifact, a trail event — can be written before or without its Run row
/// (projection ordering, the container path), and an enforced key would LOSE that data on
/// a constraint failure. So the Run.* collections are unmapped in-memory holders, populated
/// by DbRunStore through RunId queries, and each child gets a length-capped indexed column
/// instead of a relationship.
/// </para>
/// <para>
/// 2026-08-26-7a51: lifted out of the context, which was at its length ceiling. It applies
/// a CONVENTION across eight entity types rather than configuring one, which is why it is
/// an Apply method beside RunRecordIdentityConfiguration rather than an
/// IEntityTypeConfiguration.
/// </para>
/// </summary>
public sealed class RunChildConfiguration
{
    private static readonly Type[] Children =
    [
        typeof(RunRepo), typeof(RunStep), typeof(RunEvent), typeof(RunDecision),
        typeof(RunLlmCall), typeof(RunArtifact), typeof(RunSandbox), typeof(RunPhase),
    ];

    public void Apply(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<Run>().Ignore(r => r.Repos).Ignore(r => r.Steps).Ignore(r => r.Events)
            .Ignore(r => r.Decisions).Ignore(r => r.LlmCalls).Ignore(r => r.Artifacts)
            .Ignore(r => r.Sandboxes);

        foreach (var child in Children)
        {
            var entity = modelBuilder.Entity(child);
            entity.Property("RunId").HasMaxLength(PersistenceLimits.IndexedString);
            entity.HasIndex("RunId");
        }
    }
}
