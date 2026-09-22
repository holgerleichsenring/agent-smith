namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-09-22-6ad7: WHY the ticket branch holds no set — the question the reader used to
/// answer with one null for four different situations.
/// <para>
/// The split is what lets a caller tell an UNWRITTEN branch from a BROKEN one. Nothing at the
/// path is a hand-off that has not happened yet; something at the path that does not read back
/// is an operator's edit gone wrong, and substituting a copy for it is how the edit disappears.
/// </para>
/// </summary>
public enum SpecSetBranchState
{
    /// <summary>The branch carries a set and it read back.</summary>
    Answered = 0,

    /// <summary>There is no index at the spec path: nobody has written the set to the branch.</summary>
    NothingAtThePath = 1,

    /// <summary>
    /// The set could not be read: <c>set.yaml</c> does not parse, a listed phase file does not
    /// read back as a spec, or the repository carrying it is not checked out in this run.
    /// </summary>
    Unreadable = 2,
}

/// <summary>
/// 2026-09-22-6ad7: what the ticket branch answered, and why it answered nothing.
/// </summary>
/// <param name="State">Which of the three answers this is.</param>
/// <param name="Read">The set and the commit it was last written at; null unless answered.</param>
/// <param name="Why">What could not be read, in the operator's words; null unless unreadable.</param>
public sealed record SpecSetOnBranch(
    SpecSetBranchState State, SpecSetReadResult? Read, string? Why = null)
{
    /// <summary>Nothing is written at the spec path.</summary>
    public static readonly SpecSetOnBranch Nothing = new(SpecSetBranchState.NothingAtThePath, null);

    /// <summary>Something is there and this run could not read it back as a set.</summary>
    public static SpecSetOnBranch Unreadable(string why) =>
        new(SpecSetBranchState.Unreadable, null, why);

    /// <summary>The branch answered.</summary>
    public static SpecSetOnBranch Answered(SpecSetReadResult read) =>
        new(SpecSetBranchState.Answered, read);

    /// <summary>The set the branch carries, or null when it carries none.</summary>
    public SpecSet? Set => Read?.Set;
}
