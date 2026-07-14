using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Checks;

/// <summary>
/// p0324: per configured remote repo, an authenticated read (the provider's
/// ls-remote-equivalent probe) proves the token/SSH key can actually reach it —
/// otherwise the failure surfaces mid-run when checkout dies inside a sandbox.
/// Local repos have nothing to authenticate and are skipped.
/// </summary>
public sealed class RepoAccessCheck(
    IPreflightConfigSource configSource,
    ISourceProviderFactory sourceFactory) : IPreflightCheck
{
    public string Name => "repo-access";

    public string Category => "repo";

    public async Task<PreflightCheckResult> RunAsync(CancellationToken cancellationToken)
    {
        var config = configSource.Resolve().Config;
        if (config is null)
            return PreflightCheckResult.Skip("agentsmith.yml failed to load — see config-schema");

        var remotes = config.Repos.Where(r => r.Value.Type != RepoType.Local).ToList();
        if (remotes.Count == 0)
            return PreflightCheckResult.Skip("no remote repos configured (local paths need no auth)");

        var lines = new List<string>();
        var failures = new List<string>();
        foreach (var (name, repo) in remotes)
        {
            var probe = await sourceFactory.Create(repo).ProbeAsync(cancellationToken);
            if (probe.Ok) lines.Add($"{name} ({repo.Type}): ok {probe.LatencyMs}ms");
            else failures.Add($"{name} ({repo.Type}): {probe.Error}");
        }

        if (failures.Count > 0)
            return PreflightCheckResult.Fail(
                string.Join(" | ", failures),
                "Check the repo's auth secret (token/SSH key) and url — the configured credential must "
                + "be able to list the remote, or checkout fails mid-run inside the sandbox.");

        return PreflightCheckResult.Pass(string.Join(" | ", lines));
    }
}
