using AgentSmith.Application.Models.Registry;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// Guards, substitutes and writes ONE staged auth file: path allowlist first
/// (the load-bearing guard — BEFORE any write), then the secret-leak scan on the
/// raw templated content, then host-side token substitution, then the sandbox
/// write. Every rejection carries a reason for the loud per-host decision line. 2026-11-25-9992d: the feeds are the
/// run project's, handed in by the caller.
/// </summary>
public sealed class StagedAuthFileWriter(
    RegistryAuthPathGuard pathGuard,
    SecretLeakGuard leakGuard,
    RegistryTokenSubstitutor substitutor,
    ILogger<StagedAuthFileWriter> logger)
{
    public async Task<StagedFileOutcome> WriteAsync(
        string repoKey, ISandboxFileReader reader, StagedAuthFile file, IReadOnlyList<RegistryConfig> registries,
        CancellationToken ct)
    {
        var hosts = RegistryTokenPlaceholder.HostsIn(file.Content);

        var verdict = pathGuard.Check(file.Path);
        if (!verdict.IsAllowed)
            return StagedFileOutcome.Fail(hosts, verdict.Reason!);

        var leakedHosts = leakGuard.LeakedHosts(file.Content, registries);
        if (leakedHosts.Count > 0)
            return StagedFileOutcome.Fail(
                Union(hosts, leakedHosts),
                $"templated content for '{file.Path}' contained the real token for host(s) "
                + $"[{string.Join(", ", leakedHosts)}] instead of the placeholder");

        var substitution = substitutor.Substitute(file.Content, registries);
        if (!substitution.IsSuccess)
            return StagedFileOutcome.Fail(hosts, substitution.FailureReason!);

        await reader.WriteAsync(verdict.NormalizedPath!, substitution.Content!, ct);
        logger.LogInformation(
            "{Repo}: staged generic registry auth at {Path} for host(s) [{Hosts}].",
            repoKey, verdict.NormalizedPath, string.Join(", ", hosts));
        return StagedFileOutcome.Ok(verdict.NormalizedPath!, hosts);
    }

    private static IReadOnlyList<string> Union(
        IReadOnlyList<string> hosts, IReadOnlyList<string> leaked) =>
        hosts.Concat(leaked).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
