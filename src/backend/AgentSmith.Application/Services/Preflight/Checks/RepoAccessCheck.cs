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
/// <para>
/// 2026-08-27-7098: a project's OWN repos are probed too. An installation that
/// discovers its repositories through a connection declares none individually, so
/// this check skipped itself entirely on exactly the installations whose start
/// later died on an unreachable remote. The glob expander materialises each
/// discovered repository into the project as a full connection — credentials and
/// all — so probing those probes the connection, without a paginated listing per
/// connection at every startup.
/// </para>
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

        var remotes = Remotes(config);
        if (remotes.Count == 0)
            return PreflightCheckResult.Skip(
                "no remote repo or connection configured (local paths need no auth)");

        var lines = new List<string>();
        var failures = new List<string>();
        foreach (var repo in remotes)
        {
            var probe = await sourceFactory.Create(repo).ProbeAsync(cancellationToken);
            if (probe.Ok) lines.Add($"{repo.Name} ({repo.Type}): ok {probe.LatencyMs}ms");
            else failures.Add($"{repo.Name} ({repo.Type}): {probe.Error}");
        }

        if (failures.Count > 0)
            return PreflightCheckResult.Fail(
                string.Join(" | ", failures),
                "Check the repo's auth secret (token/SSH key) and url — the configured credential must "
                + "be able to list the remote, or checkout fails mid-run inside the sandbox.");

        return PreflightCheckResult.Pass(string.Join(" | ", lines));
    }

    // Deduplicated by what is actually reached: two projects sharing one connection
    // resolve the same remote twice, and probing it twice proves nothing new.
    private static IReadOnlyList<RepoConnection> Remotes(AgentSmithConfig config) =>
        [.. config.Repos.Values
            .Concat(config.Projects.Values.SelectMany(p => p.Repos))
            .Where(r => r.Type != RepoType.Local)
            .DistinctBy(Target, StringComparer.OrdinalIgnoreCase)];

    private static string Target(RepoConnection repo) =>
        $"{repo.Type}|{repo.Url ?? repo.Path ?? repo.Name}|{repo.Auth}";
}
