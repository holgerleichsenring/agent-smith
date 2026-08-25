namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0484: what the account searched, in the same grammar an agent command is reported in, so
/// the citation check needs no new reading. p0483 let the account settle an absence by
/// LOOKING and left it unable to say that it had looked: the first live run ran fifteen
/// searches and was refused with "claimed satisfied but cited nothing", because a search the
/// ACCOUNT ran is neither a path in the diff nor a command in the list.
/// <para>
/// 2026-08-25-0eae: separated from <see cref="BranchSearch"/>, which now runs two different
/// commands against two different trees — what a search MEANS afterwards is its own question,
/// and holding both put the type past the length the architecture rule allows.
/// </para>
/// </summary>
internal sealed class SearchEvidence
{
    private readonly Lock _sync = new();
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines
    {
        get { lock (_sync) return [.. _lines]; }
    }

    /// <summary>
    /// The pattern is what is cited, because the account wrote it and can reproduce it
    /// exactly. Only a search that REACHED a tree is remembered — an unknown repository or a
    /// blank pattern never ran and is evidence of nothing.
    /// <para>
    /// p0484 recorded every search that happened, so an account that looked may say so even
    /// when the look failed. 2026-08-25-0eae keeps that and adds what it left unsaid: an exit
    /// above 1 means the search could not run, and the line says so in words. Dropping the
    /// line would refuse an account for citing a search it really ran; leaving the line
    /// indistinguishable would let a broken search prove an absence, which is the one
    /// direction this mechanism must not be wrong in.
    /// </para>
    /// <para>
    /// It also names the ref it read. A base search and a branch search of one pattern are
    /// two different facts, and a citation that cannot tell them apart cannot carry a rule
    /// that depends on which one ran.
    /// </para>
    /// </summary>
    public void Remember(string repository, string pattern, int exitCode, string? baseRef = null)
    {
        var where = baseRef is null ? repository : $"{repository}@{baseRef}";
        var ran = exitCode is 0 or 1 ? string.Empty : " and could not run, so it proves nothing";
        lock (_sync) _lines.Add($"{where}: the account searched '{pattern}' exited {exitCode}{ran}");
    }
}
