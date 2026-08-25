namespace AgentSmith.Application.Models;

/// <summary>
/// p0496: what the bootstrap probe actually read — the branch, the base that branch was
/// cut from, and the paths it looked for.
/// <para>
/// The refusal used to guess ("the repo may be empty or newly renamed"), which sent an
/// operator to verify a repository name that was correct while the file sat one branch
/// away. A diagnosis that guesses is worse than one that states what it did.
/// </para>
/// </summary>
public sealed record BootstrapProbeReport(
    string? Branch, string? BaseBranch, IReadOnlyList<string> Paths)
{
    public string Describe()
    {
        var branch = Branch is null ? "the checked-out branch" : $"branch '{Branch}'";
        var cutFrom = BaseBranch is null ? string.Empty : $" (cut from '{BaseBranch}')";
        var paths = Paths.Count == 0 ? "no path" : string.Join(", ", Paths.Distinct(StringComparer.Ordinal));
        return $"Read on {branch}{cutFrom}: {paths}.";
    }
}
