using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Core.Services.Configuration;

/// <summary>
/// 2026-10-02-5f89a: the BLOCKING findings a repo, connection or tracker raises when what it
/// authenticates with cannot be found — the precedent DesignSourceCatalogBuilder set. The entry
/// itself is still built: dropping it would turn one missing secret into an unknown-reference
/// finding on every project that uses it.
/// </summary>
public static class MissingSecretFindings
{
    /// <summary>A blocking finding when <paramref name="auth"/> names no entry of the catalog, else null.</summary>
    public static StartupFinding? Check(
        string catalog, string kind, string name, string auth, IReadOnlySet<string> secrets) =>
        secrets.Contains(auth)
            ? null
            : Blocking($"{catalog}:{name}", string.IsNullOrWhiteSpace(auth)
                ? $"{kind} '{name}' names no secret in 'auth'."
                : $"{kind} '{name}' names secret '{auth}', which is not defined in secrets: catalog.");

    public static StartupFinding JiraWithoutEmail(string name) =>
        Blocking($"trackers:{name}.email",
            $"Jira tracker '{name}' has no email: set email on the tracker, or the jira_email secret "
            + "or JIRA_EMAIL it is filled from.");

    private static StartupFinding Blocking(string field, string reason) =>
        new(StartupSubsystems.Configuration, StartupFindingSeverity.Blocking, reason, Field: field);
}
