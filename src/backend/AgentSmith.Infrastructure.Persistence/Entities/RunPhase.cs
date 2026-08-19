namespace AgentSmith.Infrastructure.Persistence.Entities;

/// <summary>
/// p0466: one derived phase of a run — the thing the operator reasons in, finally a row.
/// <para>
/// Before this, a phase existed only as the "p19213a: " prefix a step name carried, so a
/// phase that had ended was not addressable by anything: no id to link to, no place to
/// hang its decisions, its steps or the spec it executed. A run viewer could therefore
/// only ever show what was still live.
/// </para>
/// </summary>
public sealed class RunPhase : EntityBase
{
    public long Id { get; set; }
    public string RunId { get; set; } = string.Empty;

    /// <summary>The derived phase id ("p19213a") — unique within the run.</summary>
    public string PhaseId { get; set; } = string.Empty;

    /// <summary>The phase's 1-based position in the derived sequence.</summary>
    public int Ordinal { get; set; }

    /// <summary>The phase's goal, as the ratified spec states it.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>not_started | in_progress | done | failed.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the phase reached a terminal standing; null while it runs.</summary>
    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// Why the standing is what it is — the failing command of a stopped phase, or the
    /// note explaining a phase that was already satisfied on entry. Null for a phase
    /// that simply ran through.
    /// </summary>
    public string? Verdict { get; set; }
}
