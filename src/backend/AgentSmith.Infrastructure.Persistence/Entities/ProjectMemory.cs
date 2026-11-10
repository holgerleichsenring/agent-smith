namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// 2026-11-10-fcd7a: one memory a project holds — tied to the project's config entity by its surrogate id, so a
/// deleted project takes its memories with it, and carrying where it came from: a conversation turn or a run's
/// spec, and who said or wrote it.
/// </summary>
public sealed class ProjectMemory : EntityBase
{
    public long Id { get; set; }

    /// <summary>ConfigEntities.Id of the project.</summary>
    public long ProjectId { get; set; }

    public string Slug { get; set; } = string.Empty;

    /// <summary>The entry type's slug: feedback, project or reference.</summary>
    public string Type { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>ProjectMemorySourceKinds: conversation or run.</summary>
    public string SourceKind { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    /// <summary>The transcript index of a conversation turn, or a run's spec id.</summary>
    public string? SourceRef { get; set; }

    public string? Author { get; set; }
}
