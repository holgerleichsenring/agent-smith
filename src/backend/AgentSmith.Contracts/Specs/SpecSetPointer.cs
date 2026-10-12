namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the MACHINE state of a spec set. Persistence splits by reader — content
/// lives in git on the ticket branch (diff, blame, history and the PR review are
/// the UI), and only what nobody reads by hand lives in the database: which repo
/// carries the set, the sha of the last revision this system wrote, and the
/// per-ticket hand-back case and counter. Whether a repeat ends the loop is read from
/// the ticket thread, not from a sha: every derivation commits a fresh revision, so
/// the sha this slot once carried never matched (2026-09-07-bd7a).
/// <para>
/// 2026-10-06-03c7c: <see cref="Key"/> is the TICKET key, and the pointer also caches the
/// series' base id. The branch is the source of that id; this copy is read only when the branch
/// carries no set.
/// </para>
/// <para>
/// 2026-10-06-03c7e: <see cref="ExecutedThrough"/> is how many leading specs of the series the
/// record step moved to done/ — what filing, which never reads the branch, needs so an amendment
/// never writes an executed spec back into planned/.
/// </para>
/// <para>
/// 2026-10-12-3a36b: <see cref="HandbackHeads"/> is <c>origin/&lt;branch&gt;</c> per scoped
/// repository when a contradiction parked — the reference a later run compares the branch
/// against to tell whether somebody else committed since. Null once a revision that is not a
/// hand-back is recorded.
/// </para>
/// <para>
/// 2026-10-12-f5f7b: <see cref="RevisionShas"/> is the last sha this system wrote PER REPOSITORY
/// of the series. A pointer without one — every row written before it — reads as
/// <c>{CarryingRepo: RevisionSha}</c> (<see cref="ShaByRepo"/>), so no row reads as an edit for
/// lack of the map.
/// </para>
/// <para>
/// 2026-10-12-f5f7d: <see cref="ExecutedIds"/> names the executed specs — a series written into
/// several repositories has no position that says what ran.
/// </para>
/// </summary>
public sealed record SpecSetPointer(
    string Key,
    string CarryingRepo,
    string RevisionSha,
    int RevisionNumber,
    SpecHandbackCase LastHandbackCase = SpecHandbackCase.None,
    int RepeatedHandbackCount = 0,
    string? SeriesId = null,
    int ExecutedThrough = 0,
    IReadOnlyDictionary<string, string>? HandbackHeads = null,
    IReadOnlyDictionary<string, string>? RevisionShas = null,
    IReadOnlyList<string>? ExecutedIds = null)
{
    /// <summary>
    /// 2026-10-12-f5f7d: the ids the record step moved to done/. A row that stored none but counts
    /// executed specs reads as that many leading specs of <paramref name="stored"/>.
    /// </summary>
    public IReadOnlyList<string> ExecutedIdsOf(IReadOnlyList<SpecPhase> stored) =>
        ExecutedIds is { Count: > 0 } ids ? ids : [.. (stored ?? []).Take(ExecutedThrough).Select(p => p.PhaseId)];

    /// <summary>The per-repository shas, back-filled from the one carrying repository when absent.</summary>
    public IReadOnlyDictionary<string, string> ShaByRepo =>
        RevisionShas is { Count: > 0 } shas
            ? shas
            : new Dictionary<string, string>(StringComparer.Ordinal) { [CarryingRepo] = RevisionSha };
}

/// <summary>
/// p0393a: a set found on the ticket branch, with the sha of the last commit that
/// touched its directory. The caller compares that sha against the pointer this
/// system recorded to tell its OWN last revision apart from a reviewer's edit —
/// the cause the next revision names.
/// <para>
/// 2026-10-12-f5f7b: a set joined from several repositories' sub-series carries the last commit per
/// repository it was read from (<see cref="RevisionShas"/>; <see cref="LastCommitSha"/> is the
/// carrying repository's, else the newest manifest's), and the members whose sub-series lags the
/// newest revision (<see cref="Lagging"/>) — read, named, and never joined.
/// </para>
/// <para>
/// 2026-10-12-f5f7f: a lagging read also carries <see cref="OutOfStepPrevious"/> — every member's
/// done/ specs in id order, then the newest members' unexecuted specs — the set a re-cut demand cuts
/// from, so what already ran stays its executed prefix.
/// </para>
/// </summary>
public sealed record SpecSetReadResult(SpecSet Set, string LastCommitSha)
{
    public IReadOnlyDictionary<string, string> RevisionShas { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public IReadOnlyList<string> Lagging { get; init; } = [];

    public SpecSet? OutOfStepPrevious { get; init; }
}
