namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// 2026-11-25-0873: what makes a <c>machine:</c> block unable to act — no name or email, an entry without a secret, an App
/// without its installation, a service principal half set (2026-11-25-d0e2) — as the key to fix under
/// <c>prefix</c> (<c>machine</c> on a project, <c>pocs.machine</c> for the PoC area) and the sentence that says it. One rule for
/// every place a block is written; none for an absent block or one naming no hoster.
/// </summary>
public static class MachineIdentityProblems
{
    public static IEnumerable<(string Field, string Text)> Of(MachineIdentityConfig? machine, string prefix)
    {
        if (machine is null) yield break;
        var entries = new[] { (RepoType.GitHub, machine.Github), (RepoType.GitLab, machine.Gitlab), (RepoType.AzureDevOps, machine.AzureDevops) }
            .Where(e => e.Item2 is not null).Select(e => (Hoster: e.Item1, Entry: e.Item2!)).ToList();
        if (entries.Count == 0) yield break;
        if (string.IsNullOrWhiteSpace(machine.Name))
            yield return ($"{prefix}.name", $"{prefix}.name is empty — the name agent-smith's commits and acts carry.");
        foreach (var problem in entries.SelectMany(e => Entry(machine, prefix, e.Hoster, e.Entry).Concat(Principal($"{prefix}.{Key(e.Hoster)}", e.Hoster, e.Entry))))
            yield return problem;
    }

    public static string Key(RepoType hoster) => hoster switch
    {
        RepoType.GitHub => "github",
        RepoType.GitLab => "gitlab",
        _ => "azure_devops",
    };

    private static IEnumerable<(string, string)> Entry(MachineIdentityConfig machine, string prefix, RepoType hoster, MachineHosterConfig entry)
    {
        var key = $"{prefix}.{Key(hoster)}";
        if (string.IsNullOrWhiteSpace(entry.Auth)) yield return ($"{key}.auth", $"{key}.auth names no secret.");
        if (string.IsNullOrWhiteSpace(machine.Email) && string.IsNullOrWhiteSpace(entry.Email))
            yield return ($"{prefix}.email", $"{key} has no commit email — set {prefix}.email or {key}.email.");
        if (!string.IsNullOrWhiteSpace(entry.AppId) && hoster != RepoType.GitHub) yield return ($"{key}.app_id", $"{key}.app_id is for a GitHub App only.");
        if (!string.IsNullOrWhiteSpace(entry.AppId) && string.IsNullOrWhiteSpace(entry.InstallationId))
            yield return ($"{key}.installation_id", $"{key}.app_id is set without installation_id — the App's installation on the repositories' owner.");
    }

    // 2026-11-25-d0e2: a service principal needs both its tenant and its client id, and exists on Azure DevOps only.
    private static IEnumerable<(string, string)> Principal(string key, RepoType hoster, MachineHosterConfig entry)
    {
        bool tenant = !string.IsNullOrWhiteSpace(entry.TenantId), client = !string.IsNullOrWhiteSpace(entry.ClientId);
        if (!tenant && !client) yield break;
        if (hoster != RepoType.AzureDevOps)
            yield return ($"{key}.tenant_id", $"{key}.tenant_id and client_id are for an Azure DevOps service principal only.");
        else if (tenant != client)
            yield return ($"{key}.{(tenant ? "client_id" : "tenant_id")}",
                $"{key} names a service principal without its {(tenant ? "client_id" : "tenant_id")} — both come from the app registration in Microsoft Entra.");
        if (!string.IsNullOrWhiteSpace(entry.AppId)) yield return ($"{key}.app_id", $"{key} is either a GitHub App or a service principal, not both.");
    }
}
