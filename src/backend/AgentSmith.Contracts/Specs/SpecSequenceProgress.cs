namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: which specs of the sequence are through and which are not.
/// <para>
/// A stopped sequence is a HALF-MIGRATED repository, which is the dangerous state
/// a migration has — worse than not having started. The stop is therefore only half
/// the mechanism: the pull request must carry this table and must be unmergeable by
/// construction. Without it the stop produces exactly the failure it exists to
/// prevent, and it produces it in a form that looks finished.
/// </para>
/// </summary>
public sealed record SpecSequenceProgress(IReadOnlyList<SpecProgress> Specs)
{
    /// <summary>2026-10-06-03c7e: a spec already in done/ is seeded Done — it ran in an earlier run. 2026-11-27-3532d: a
    /// retired spec is seeded Superseded.</summary>
    public static SpecSequenceProgress ForSet(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var retired = set.Retired();
        return new([.. set.Specs.Select(p => new SpecProgress(p.SpecId, p.Draft.Goal,
            set.Executed.Contains(p.SpecId, StringComparer.Ordinal) ? SpecRunState.Done
            : retired.Contains(p.SpecId) ? SpecRunState.Superseded : SpecRunState.NotStarted))]);
    }

    /// <summary>True while any spec is anything other than done — the half-migrated state. A superseded spec is settled.</summary>
    public bool IsPartial => Specs.Any(p => !p.IsSettled);

    /// <summary>2026-11-23-df05: no spec was recorded done over a branch that carries work — the run delivered no spec.</summary>
    public bool DeliveredNone => !Specs.Any(p => p.State == SpecRunState.Done && p.CarriedWork);

    /// <summary>2026-10-13-8762u: the attempt ended at a boundary — at least one spec is left to the next attempt and
    /// every other spec is done; a chunk boundary and a review-share stop alike.</summary>
    public bool StoppedAtChunk =>
        Specs.Any(p => p.NextAttempt) && Specs.All(p => p.NextAttempt || p.IsSettled);

    /// <summary>2026-10-13-8762g: the specs this attempt does not build, seeded not started for the next
    /// attempt with <paramref name="reason"/> as their note — the one way a step ends an attempt at a boundary.</summary>
    public SpecSequenceProgress Deferring(IReadOnlyCollection<string> ids, string reason) =>
        new([.. Specs.Select(p => ids.Contains(p.SpecId)
            ? p with { State = SpecRunState.NotStarted, Note = reason, NextAttempt = true }
            : p)]);

    /// <summary>2026-11-27-3532d: <paramref name="retired"/> superseded with <paramref name="note"/>, and the row of its
    /// replacement right behind it, in progress — where the set places the replacement.</summary>
    public SpecSequenceProgress Superseding(string retired, string replacement, string goal, string note) =>
        new([.. Specs.Where(p => p.SpecId != replacement).SelectMany(p => p.SpecId == retired
            ? new[] { p with { State = SpecRunState.Superseded, Note = note }, new SpecProgress(replacement, goal, SpecRunState.InProgress) }
            : [p])]);

    public SpecSequenceProgress With(
        string specId, SpecRunState state, string? failingCommand = null, string? note = null, bool carriedWork = false) =>
        new([.. Specs.Select(p => p.SpecId == specId
            ? p with
            {
                State = state,
                FailingCommand = failingCommand ?? p.FailingCommand,
                Note = note ?? p.Note,
                CarriedWork = carriedWork,
            }
            : p)]);
}

/// <summary>p0393a: one spec's standing in the sequence.</summary>
/// <param name="Note">p0460: why the standing is what it is, where the state alone would
/// mislead — a spec found already satisfied on entry is DONE and did no work, and a
/// reader of the table has to be able to tell that from a spec that ran.</param>
/// <param name="NextAttempt">2026-10-13-8762g: the spec is left to the next attempt, never built in this one.</param>
/// <param name="CarriedWork">2026-11-23-df05: verification recorded the spec over a branch that carries source changes.</param>
public sealed record SpecProgress(
    string SpecId,
    string Goal,
    SpecRunState State,
    string? FailingCommand = null,
    string? Note = null,
    bool NextAttempt = false,
    bool CarriedWork = false)
{
    /// <summary>2026-11-27-3532d: done, or superseded — nothing is left to build of it.</summary>
    public bool IsSettled => State is SpecRunState.Done or SpecRunState.Superseded;
}

/// <summary>p0393a: the states a reviewer must be able to tell apart.</summary>
public enum SpecRunState
{
    NotStarted = 0,
    InProgress = 1,
    Done = 2,

    /// <summary>The spec ran and its verification came back red.</summary>
    Failed = 3,

    /// <summary>
    /// 2026-09-17-0e79c: the spec did NOT run — it was handed back before its work started,
    /// because what the specification says it rests on is no longer so.
    /// <para>
    /// A separate state, not a flavour of <see cref="Failed"/>, because the two ask opposite
    /// things of the operator: a red build is fixed by working the code, and a false premise is
    /// fixed by amending the specification. A reader that had to tell them apart by parsing the
    /// verdict string would be deriving a fact the producer already knows.
    /// </para>
    /// </summary>
    HandedBack = 4,

    /// <summary>2026-11-27-3532d: the spec was retired undelivered; the spec that supersedes it is built in its place.
    /// Settled like done — a retired spec is no shortfall — and never built.</summary>
    Superseded = 5,
}
