using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0496: what the bootstrap probe actually read — the branch, the base that branch was
/// cut from, and the paths it looked for.
/// <para>
/// The refusal used to guess ("the repo may be empty or newly renamed"), which sent an
/// operator to verify a repository name that was correct while the file sat one branch
/// away. A diagnosis that guesses is worse than one that states what it did.
/// </para>
/// <para>
/// 2026-09-01-eec0: the same reason carries the rename. A repository initialised before
/// the principles file was named for what it holds still has the old file, and "file
/// missing" would send the operator looking for something they already wrote.
/// </para>
/// <para>
/// 2026-09-04-ae3a: and the same reason again, one level down. Folded over a sandbox, the
/// rename sentence was told to a repository whose FIRST context had already been migrated —
/// false of that context, and its remedy was the run that had skipped the other one. The
/// missing files are carried per context and the sentence goes with the context it is true of.
/// </para>
/// </summary>
public sealed record BootstrapProbeReport(
    string? Branch, string? BaseBranch, IReadOnlyList<string> Paths,
    IReadOnlyList<MissingBootstrapFile>? Missing = null)
{
    public string Describe()
    {
        var branch = Branch is null ? "the checked-out branch" : $"branch '{Branch}'";
        var cutFrom = BaseBranch is null ? string.Empty : $" (cut from '{BaseBranch}')";
        var paths = Paths.Count == 0 ? "no path" : string.Join(", ", Paths.Distinct(StringComparer.Ordinal));
        return $"Read on {branch}{cutFrom}: {paths}.{MissingText()}";
    }

    private string MissingText() =>
        Missing is not { Count: > 0 }
            ? string.Empty
            : " Missing per context: " + string.Join("; ", Missing.Select(m => m.Describe())) + ".";
}
