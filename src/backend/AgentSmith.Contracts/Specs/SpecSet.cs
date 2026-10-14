using System.Text.Json.Serialization;

namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the ordered set of specs derived from one ticket, carried on the
/// ticket branch.
/// <para>
/// ONE TICKET IS NOT ONE SPEC. An 800-line migration manual across several
/// repositories is a sequence, and the sequence is where termination comes from:
/// each spec ends at its own done-list and its own VerifySpec, so stopping is
/// structural instead of a judgement the model has to reach.
/// </para>
/// </summary>
/// <param name="TicketFingerprint">2026-09-08-5cd2: fingerprint of the ticket text the model
/// last cut this set from. Null on a set cut before it existed, which compares as unchanged —
/// a missing fact is not evidence of an edit.</param>
/// <param name="Approval">2026-09-17-0e79a: the approval this set was ratified by in the design
/// conversation, or null for a set nobody approved. On a set read back from the branch it is the
/// approval the published revision came from; the branch's set wins over any stored record
/// whatever the two instants say (2026-09-22-6ad7).</param>
/// <param name="Series">2026-10-06-03c7c: the series' code-minted base id
/// (<c>{yyyy-MM-dd}-{xxxx}</c>); its specs are the base plus a letter. Null only on a set not yet
/// stamped — a set read off a branch without one is treated as absent.</param>
public sealed record SpecSet(
    string Key,
    IReadOnlyList<DerivedSpec> Specs,
    SpecAccounting Accounting,
    IReadOnlyList<SpecRevision> Revisions,
    SpecSource Source,
    SpecHandback? Handback = null,
    bool TicketPinnedWhole = false,
    IReadOnlyList<string>? ExecutedSpecIds = null,
    string? TicketFingerprint = null,
    SpecApproval? Approval = null,
    string? Series = null)
{
    /// <summary>
    /// Spec ids that already ran — on this branch, in this run or an earlier one.
    /// An executed spec is APPEND-ONLY: editing one would rewrite the record of work
    /// that already happened and already sits in the branch history, so a correction
    /// to executed work is a NEW spec, never a changed one.
    /// </summary>
    public IReadOnlyList<string> Executed { get; init; } = ExecutedSpecIds ?? [];

    /// <summary>The specs no run has started yet — the only ones a correction may re-cut.</summary>
    [JsonIgnore]
    public IReadOnlyList<DerivedSpec> UnexecutedTail =>
        [.. Specs.Where(p => !Executed.Contains(p.SpecId, StringComparer.Ordinal))];

    /// <summary>
    /// The executed specs in their original order. Only a CONTIGUOUS head can be
    /// preserved by position, which is what the append-only rule already implies: the
    /// sequence executes in order, so anything executed is a prefix of it.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<DerivedSpec> ExecutedHead =>
        [.. Specs.TakeWhile(p => Executed.Contains(p.SpecId, StringComparer.Ordinal))];

    /// <summary>
    /// Cap on the specs one ticket may split into. Beyond this cap a ticket is a
    /// programme and belongs in the spec dialogue, not in one coding run.
    /// <para>
    /// 2026-09-17-0e79e: the executor's command-execution guard is no longer a fixed
    /// number this cap has to fit under — the budget follows the spec count
    /// (<see cref="Pipeline.StepBudget"/>), so raising the cap raises the guard with it.
    /// What the cap still decides is how much work one run may be asked to do.
    /// </para>
    /// </summary>
    public const int MaxSpecs = 8;

    /// <summary>2026-10-06-03c7d: the series' goal the manifest carries — an epic's own goal, or
    /// null when nothing set one.</summary>
    public string? Goal { get; init; }

    /// <summary>2026-10-12-f5f7d: the repositories whose sub-series a write stamped — the
    /// manifest's <c>series_repos</c>; null on a set no write placed yet.</summary>
    public IReadOnlyList<string>? SeriesRepos { get; init; }

    /// <summary>2026-10-13-8762j: the series contract the manifest carries — series-wide like the goal;
    /// null when none was declared.</summary>
    public SeriesContract? Contract { get; init; }

    /// <summary>2026-10-12-f5f7d: the sub-series of <paramref name="repo"/> — only the specs that
    /// name it, every series-wide field unchanged.</summary>
    public SpecSet OfRepository(string repo)
    {
        var specs = Specs.Where(p => p.Draft.Repos is [var named]
            && string.Equals(named, repo, StringComparison.OrdinalIgnoreCase)).ToList();
        return this with { Specs = specs, Executed = [.. Executed.Where(id => specs.Any(p => p.SpecId == id))] };
    }

    /// <summary>The revision the run works from — always the latest.</summary>
    [JsonIgnore]
    public SpecRevision Current => Revisions[^1];

    /// <summary>True when derivation handed the ticket back instead of specifying it.</summary>
    [JsonIgnore]
    public bool IsHandedBack => Handback is not null && Handback.Case != SpecHandbackCase.None;
}

/// <summary>
/// p0393a: the header of one revision. Re-entry never regenerates the set — it
/// reads the last revision and writes a new one naming its CAUSE ("comment on
/// ticket", "reviewer edit in PR #N", "re-cut", "resume"). The transform is an LLM
/// and will never be deterministic; reproducibility comes from not repeating it,
/// not from expecting the same answer twice.
/// </summary>
public sealed record SpecRevision(int Number, string Cause, DateTimeOffset At);
