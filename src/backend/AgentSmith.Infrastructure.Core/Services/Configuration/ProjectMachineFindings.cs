using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-11-25-9992h: what a project's setup says about the identity agent-smith acts under — one rule for the running
/// configuration (<see cref="ResolvedProjectBuilder"/>) and the studio's draft (<c>ConfigDraftRules</c>). A hoster the project
/// has repositories on and no machine identity for is ADVISORY: its acts go out under each repository's own token, and the
/// finding says so rather than letting the fallback pass in silence. A block that cannot act — no name or email, an entry
/// without a secret, an App without its installation — is BLOCKING on the key to fix. At load the blocking ones are resolution
/// findings (the project drops out) and the advisory ones are recorded beside them, never refused on.
/// </summary>
public static class ProjectMachineFindings
{
    /// <summary>Every finding — what the studio shows for a draft.</summary>
    public static IEnumerable<StartupFinding> For(
        string project, MachineIdentityConfig? machine, IEnumerable<(string Name, RepoType Type)> repos) =>
        [.. Unusable(project, machine), .. Missing(project, machine, repos)];

    /// <summary>The advisory findings of every resolved project, from its stamped repos.</summary>
    public static IEnumerable<StartupFinding> Missing(IEnumerable<ResolvedProject> projects) =>
        projects.SelectMany(p => Missing(p.Name, p.Machine, p.Repos.Select(r => (r.Name, r.Type))));

    /// <summary>One advisory finding per hoster the project has repositories on and no identity for.</summary>
    public static IEnumerable<StartupFinding> Missing(
        string project, MachineIdentityConfig? machine, IEnumerable<(string Name, RepoType Type)> repos)
    {
        foreach (var hoster in repos.Where(r => r.Type != RepoType.Local).GroupBy(r => r.Type))
            if (machine?.For(hoster.Key) is null)
                yield return Missing(project, hoster.Key, [.. hoster.Select(r => r.Name).Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>The blocking findings of a block that cannot act; none for an absent block (rules: <see cref="MachineIdentityProblems"/>).</summary>
    public static IEnumerable<StartupFinding> Unusable(string project, MachineIdentityConfig? machine) =>
        MachineIdentityProblems.Of(machine, "machine").Select(p => ProjectFindings.Blocking(project, p.Field, $"Project '{project}': {p.Text}"));

    private static StartupFinding Missing(string project, RepoType hoster, IReadOnlyList<string> repos) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
            $"Project '{project}' has no machine identity for {Label(hoster)}: agent-smith pushes, opens, comments on and merges "
            + $"pull requests in {string.Join(", ", repos)} with each repository's own token, as whoever owns it. "
            + $"Set machine.{Key(hoster)} in the project's settings.",
            project, Field: "machine");

    internal static string Key(RepoType hoster) => MachineIdentityProblems.Key(hoster);

    private static string Label(RepoType hoster) => hoster switch
    {
        RepoType.GitHub => "GitHub",
        RepoType.GitLab => "GitLab",
        _ => "Azure DevOps",
    };
}
