using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0504: every configured repository's contexts are read remotely and each declared
/// <c>meta.domain</c> is looked up in the resolved skills catalog. The same question the
/// sandbox coordinator refuses on, answered WITHOUT a run — because a stale pin and a
/// typo look identical from a failed run, and an operator should not need to burn one
/// to tell them apart.
/// </summary>
public sealed class ContextDomainCheck(
    IPreflightConfigSource configSource,
    ISandboxLanguageResolver contextResolver,
    IDomainProfileCatalog catalog) : IPreflightCheck
{
    public string Name => "context-domain";

    public string Category => "repo";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var config = configSource.Resolve().Config;
        if (config is null)
            return PreflightCheckResult.Skip("agentsmith.yml failed to load — see config-schema");

        var declared = new List<string>();
        var unknown = new List<string>();
        foreach (var (name, repo) in config.Repos)
            await InspectAsync(name, repo, declared, unknown, cancellationToken);

        if (unknown.Count > 0)
            return PreflightCheckResult.Fail(
                string.Join(" | ", unknown),
                $"The resolved catalog at '{catalog.Origin}' carries "
                + (catalog.KnownDomains.Count == 0
                    ? "no domain profiles"
                    : $"[{string.Join(", ", catalog.KnownDomains)}]")
                + ". Fix the declared domain, or move the skills pin to a release that ships it — "
                + "a run touching that repository is refused before any sandbox is created.");

        return declared.Count == 0
            ? PreflightCheckResult.Skip("no context declares meta.domain")
            : PreflightCheckResult.Pass(string.Join(" | ", declared));
    }

    private async Task InspectAsync(
        string name, RepoConnection repo, List<string> declared, List<string> unknown,
        CancellationToken ct)
    {
        IReadOnlyList<RemoteContextDiscovery> contexts;
        try { contexts = await contextResolver.ResolveAllAsync(repo, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            unknown.Add($"{name}: contexts could not be read — {ex.Message}");
            return;
        }

        foreach (var context in contexts)
        {
            if (string.IsNullOrWhiteSpace(context.Domain)) continue;
            if (catalog.Find(context.Domain) is { } profile)
                declared.Add($"{name}/{context.ContextName}: domain '{profile.Name}' -> {profile.Image}");
            else
                unknown.Add($"{name}/{context.ContextName}: unknown domain '{context.Domain}'");
        }
    }
}
