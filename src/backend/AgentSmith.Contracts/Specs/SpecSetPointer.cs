namespace AgentSmith.Contracts.Specs;

/// <summary>
/// p0393a: the MACHINE state of a spec set. Persistence splits by reader — content
/// lives in git on the ticket branch (diff, blame, history and the PR review are
/// the UI), and only what nobody reads by hand lives in the database: which repo
/// carries the set, the sha of the last revision this system wrote, and the
/// per-ticket hand-back counters that end a non-progressing loop.
/// </summary>
public sealed record SpecSetPointer(
    string Key,
    string CarryingRepo,
    string RevisionSha,
    int RevisionNumber,
    SpecHandbackCase LastHandbackCase = SpecHandbackCase.None,
    int RepeatedHandbackCount = 0,
    string? HandbackSourceSha = null);

/// <summary>
/// p0393a: a set found on the ticket branch, with the sha of the last commit that
/// touched its directory. The caller compares that sha against the pointer this
/// system recorded to tell its OWN last revision apart from a reviewer's edit —
/// the cause the next revision names.
/// </summary>
public sealed record SpecSetReadResult(SpecSet Set, string LastCommitSha);

/// <summary>
/// p0393a: the outcome of committing one revision. A failed write leaves the run
/// working from the in-memory set: the artifacts are how a REVIEWER reads the
/// derivation, and losing the reviewer's view must not lose the run.
/// </summary>
public sealed record SpecSetWriteResult(bool Written, string? CommitSha, string? Error)
{
    public static SpecSetWriteResult Ok(string sha) => new(true, sha, null);

    public static SpecSetWriteResult Failed(string error) => new(false, null, error);
}
