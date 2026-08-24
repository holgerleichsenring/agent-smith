using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// p0504: turns a context's declared <c>meta.domain</c> into the profile it names,
/// and refuses the run when the resolved catalog carries no such profile.
/// <para>
/// The refusal happens where the value first exists with no pod created — after the
/// contexts are known, before a sandbox spec is built. Degrading to "no domain"
/// would silently drop the gate this phase exists to install, so a stale pin and a
/// typo are the SAME refusal; what makes it survivable is the message naming the
/// value, the repository and context that carried it, and the catalog's resolved
/// source, because with four source modes "the pin" names nothing checkable.
/// </para>
/// </summary>
public sealed class ContextDomainResolver(
    IDomainProfileCatalog catalog,
    ILogger<ContextDomainResolver> logger)
{
    public DomainProfile? Resolve(string? repoName, RemoteContextDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);
        if (string.IsNullOrWhiteSpace(discovery.Domain)) return null;

        var profile = catalog.Find(discovery.Domain)
            ?? throw new ConfigurationException(Refusal(repoName, discovery));
        WarnOnImageMismatch(repoName, discovery, profile);
        return profile;
    }

    private string Refusal(string? repoName, RemoteContextDiscovery discovery)
    {
        var known = catalog.KnownDomains;
        return $"Unknown domain '{discovery.Domain}' declared in "
            + $"{Where(repoName)}/.agentsmith/contexts/{discovery.ContextName}/context.yaml. "
            + $"The resolved skills catalog at '{catalog.Origin}' carries "
            + (known.Count == 0 ? "no domain profiles" : $"[{string.Join(", ", known)}]")
            + ". Either the domain is a typo or the pinned catalog predates the profile — "
            + "no sandbox is started for it.";
    }

    // The operator's standing rule: the image named in the repository's context.yaml is
    // the image that gets used. A profile that does not know that image therefore does
    // NOT override it — but the disagreement is legible in one file, so it is reported
    // here rather than discovered as "command not found" inside a running sandbox.
    private void WarnOnImageMismatch(
        string? repoName, RemoteContextDiscovery discovery, DomainProfile profile)
    {
        var declared = discovery.ToolchainImage?.Trim();
        if (string.IsNullOrEmpty(declared)) return;
        if (string.Equals(declared, profile.Image, StringComparison.Ordinal)) return;
        if (profile.CompatibleImages.Contains(declared, StringComparer.Ordinal)) return;

        logger.LogWarning(
            "{Where}/{Context}: declares domain '{Domain}' and stack.image '{Declared}', which the "
            + "profile does not list as compatible (its own image is '{ProfileImage}'). The declared "
            + "image WINS and is what the sandbox runs; the profile's commands may not exist in it.",
            Where(repoName), discovery.ContextName, profile.Name, declared, profile.Image);
    }

    private static string Where(string? repoName) =>
        string.IsNullOrEmpty(repoName) ? "(default)" : repoName;
}
