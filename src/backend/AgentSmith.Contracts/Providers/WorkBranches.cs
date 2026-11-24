namespace AgentSmith.Contracts.Providers;

/// <summary>
/// 2026-11-17-713eb: the branches the framework owns — every run's work branch and the init
/// branch live under one prefix. A source provider deletes such a branch once its pull request
/// merges, and only such a branch; a person's branch is never deleted.
/// </summary>
public static class WorkBranches
{
    public const string Prefix = "agent-smith";

    /// <summary>2026-11-24-f9c5c: the prefix of every series branch (agent-smith/series/&lt;id&gt;).</summary>
    public const string SeriesPrefix = Prefix + "/series/";

    /// <summary>2026-11-24-f9c5c: whether a branch is a series branch; a <c>refs/heads/</c> prefix is ignored.</summary>
    public static bool IsSeries(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch)) return false;
        const string heads = "refs/heads/";
        var name = branch.StartsWith(heads, StringComparison.Ordinal) ? branch[heads.Length..] : branch;
        return name.Length > SeriesPrefix.Length && name.StartsWith(SeriesPrefix, StringComparison.Ordinal);
    }

    /// <summary>True for a branch name under <c>agent-smith/</c>; a <c>refs/heads/</c> prefix is ignored.</summary>
    public static bool Owns(string? branch)
    {
        if (string.IsNullOrWhiteSpace(branch)) return false;
        const string heads = "refs/heads/";
        var name = branch.StartsWith(heads, StringComparison.Ordinal) ? branch[heads.Length..] : branch;
        return name.Length > Prefix.Length + 1 && name.StartsWith(Prefix + "/", StringComparison.Ordinal);
    }
}
