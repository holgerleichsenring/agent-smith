using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// p0428: says which configured registries carry no secret, an hour before the 401.
/// <para>
/// A registry whose token resolved to nothing is staged as an empty credential, and the
/// feed answers 401 later inside a build log — where it reads as a broken package, not
/// as an environment variable nobody set. The check names the HOSTS and never the
/// values: a preflight that leaks a token into a run record is a worse defect than the
/// one it prevents.
/// </para>
/// <para>
/// A REPORT, not a gate, and the harness is what settled that: 14 healthy preset runs
/// were refused over a fixture registry no repo in them referenced. Whether an
/// unresolved token MATTERS depends on whether a repo's nuget.config / .npmrc actually
/// names that host — knowable only one step later, when SetupRegistryAuth reads those
/// files, which is also why that handler has always tolerated a missing token and
/// logged it. "Probably wrong" earns a warning; only "unambiguously wrong" earns a
/// refusal.
/// </para>
/// </summary>
public sealed class RegistryCredentialCheck(AgentSmithConfig config) : IRunPreflightCheck
{
    public string Name => "registry-credentials";

    public Task<RunPreflightFinding> RunAsync(PipelineContext pipeline, CancellationToken cancellationToken)
    {
        if (config.Registries.Count == 0)
            return Task.FromResult(RunPreflightFinding.Pass(
                Name, "no registries configured — nothing to stage"));

        var empty = config.Registries
            .Where(r => string.IsNullOrWhiteSpace(r.Token))
            .Select(Describe)
            .ToList();

        return Task.FromResult(empty.Count == 0
            ? RunPreflightFinding.Pass(Name, $"{config.Registries.Count} registry credential(s) present")
            : RunPreflightFinding.Warn(
                Name,
                $"configured registry credential(s) resolved to nothing: {string.Join(", ", empty)}"
                + " — set the environment variable behind registries[].token, or drop the entry if"
                + " the feed is public; a 401 from one of these hosts later has its cause here"));
    }

    private static string Describe(RegistryConfig registry) =>
        string.IsNullOrWhiteSpace(registry.Host) ? "(unnamed host)" : registry.Host;
}
