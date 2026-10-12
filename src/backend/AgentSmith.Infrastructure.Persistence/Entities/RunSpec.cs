namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0466: one spec a run executes — the thing the operator reasons in, finally a row.
/// 2026-10-06-03c7g: was RunPhase; the table and its key column were renamed in place.
/// <para>
/// Before this, a spec existed only as the "p19213a: " prefix a step name carried, so a
/// spec that had ended was not addressable by anything: no id to link to, no place to
/// hang its decisions, its steps or the spec it executed. A run viewer could therefore
/// only ever show what was still live.
/// </para>
/// </summary>
public sealed class RunSpec : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>The spec id ("p19213a") — unique within the run.</summary>
    public string SpecId { get; set; } = string.Empty;

    /// <summary>The spec's 1-based position in the derived sequence.</summary>
    public int Ordinal { get; set; }

    /// <summary>The spec's goal, as the ratified spec states it.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>not_started | in_progress | done | failed.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the spec reached a terminal standing; null while it runs.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Why the standing is what it is — the failing command of a stopped spec, or the
    /// note explaining a spec that was already satisfied on entry. Null for a spec
    /// that simply ran through.
    /// </summary>
    public string? Verdict { get; set; }
}
