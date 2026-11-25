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

    /// <summary>The blocking findings of a block that cannot act; none for an absent block.</summary>
    public static IEnumerable<StartupFinding> Unusable(string project, MachineIdentityConfig? machine)
    {
        if (machine is null) yield break;
        var entries = new[] { (RepoType.GitHub, machine.Github), (RepoType.GitLab, machine.Gitlab), (RepoType.AzureDevOps, machine.AzureDevops) }
            .Where(e => e.Item2 is not null).Select(e => (Hoster: e.Item1, Entry: e.Item2!)).ToList();
        if (entries.Count == 0) yield break;
        if (string.IsNullOrWhiteSpace(machine.Name))
            yield return ProjectFindings.Blocking(project, "machine.name",
                $"Project '{project}': machine.name is empty — the name agent-smith's commits and acts carry.");
        foreach (var finding in entries.SelectMany(e => Entry(project, machine, e.Hoster, e.Entry).Concat(Principal(project, e.Hoster, e.Entry))))
            yield return finding;
    }

    private static IEnumerable<StartupFinding> Entry(string project, MachineIdentityConfig machine, RepoType hoster, MachineHosterConfig entry)
    {
        var key = $"machine.{Key(hoster)}";
        if (string.IsNullOrWhiteSpace(entry.Auth))
            yield return ProjectFindings.Blocking(project, $"{key}.auth", $"Project '{project}': {key}.auth names no secret.");
        if (string.IsNullOrWhiteSpace(machine.Email) && string.IsNullOrWhiteSpace(entry.Email))
            yield return ProjectFindings.Blocking(project, "machine.email",
                $"Project '{project}': {key} has no commit email — set machine.email or {key}.email.");
        if (!string.IsNullOrWhiteSpace(entry.AppId) && hoster != RepoType.GitHub)
            yield return ProjectFindings.Blocking(project, $"{key}.app_id", $"Project '{project}': {key}.app_id is for a GitHub App only.");
        if (!string.IsNullOrWhiteSpace(entry.AppId) && string.IsNullOrWhiteSpace(entry.InstallationId))
            yield return ProjectFindings.Blocking(project, $"{key}.installation_id",
                $"Project '{project}': {key}.app_id is set without installation_id — the App's installation on the repositories' owner.");
    }

    // 2026-11-25-d0e2: a service principal needs both its tenant and its client id, and exists on Azure DevOps only.
    private static IEnumerable<StartupFinding> Principal(string project, RepoType hoster, MachineHosterConfig entry)
    {
        var key = $"machine.{Key(hoster)}";
        bool tenant = !string.IsNullOrWhiteSpace(entry.TenantId), client = !string.IsNullOrWhiteSpace(entry.ClientId);
        if (!tenant && !client) yield break;
        if (hoster != RepoType.AzureDevOps)
            yield return ProjectFindings.Blocking(project, $"{key}.tenant_id",
                $"Project '{project}': {key}.tenant_id and client_id are for an Azure DevOps service principal only.");
        else if (tenant != client)
            yield return ProjectFindings.Blocking(project, $"{key}.{(tenant ? "client_id" : "tenant_id")}",
                $"Project '{project}': {key} names a service principal without its {(tenant ? "client_id" : "tenant_id")} — both come from the app registration in Microsoft Entra.");
        if (!string.IsNullOrWhiteSpace(entry.AppId))
            yield return ProjectFindings.Blocking(project, $"{key}.app_id", $"Project '{project}': {key} is either a GitHub App or a service principal, not both.");
    }

    private static StartupFinding Missing(string project, RepoType hoster, IReadOnlyList<string> repos) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Advisory,
            $"Project '{project}' has no machine identity for {Label(hoster)}: agent-smith pushes, opens, comments on and merges "
            + $"pull requests in {string.Join(", ", repos)} with each repository's own token, as whoever owns it. "
            + $"Set machine.{Key(hoster)} in the project's settings.",
            project, Field: "machine");

    internal static string Key(RepoType hoster) => hoster switch
    {
        RepoType.GitHub => "github",
        RepoType.GitLab => "gitlab",
        _ => "azure_devops",
    };

    private static string Label(RepoType hoster) => hoster switch
    {
        RepoType.GitHub => "GitHub",
        RepoType.GitLab => "GitLab",
        _ => "Azure DevOps",
    };
}
