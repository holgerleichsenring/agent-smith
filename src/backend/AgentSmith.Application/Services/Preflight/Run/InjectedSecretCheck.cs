using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Application.Services.Sandbox;

namespace AgentSmith.Application.Services.Preflight.Run;

/// <summary>
/// 2026-08-28-b630: a declared credential actually arrived inside the sandbox that will use
/// it, asserted by name.
/// <para>
/// A backend that projects nothing reports NOT INJECTED rather than a failure. Only the pod
/// builder consumes the resolved secrets; the docker and in-process sandboxes carry no
/// secret handling at all, so failing there would turn every docker-tier run of a repository
/// that declares a credential red for a reason that has nothing to do with the repository.
/// </para>
/// </summary>
public sealed class InjectedSecretCheck(ISandboxSecretPresenceProbe probe) : IRunPreflightCheck
{
    private const string Lever =
        "the pod started, so the Secret resolved — check that the named key holds a value "
        + "and that the operator's Secret carries the entry sandbox.secrets names";

    public string Name => "injected-secrets";

    public async Task<RunPreflightFinding> RunAsync(
        PipelineContext pipeline, CancellationToken cancellationToken)
    {
        var sandboxes = Sandboxes(pipeline);
        var injecting = Injecting(sandboxes);
        if (injecting.Count > 0) return await ProbeAsync(injecting, cancellationToken);

        var declared = DeclaredCount(pipeline);
        return declared == 0
            ? RunPreflightFinding.Pass(Name, "no credentials are declared — nothing to prove")
            : RunPreflightFinding.Warn(Name, NotInjected(declared, sandboxes.Count));
    }

    private async Task<RunPreflightFinding> ProbeAsync(
        IReadOnlyList<(string Key, ISandbox Sandbox, ResolvedSandboxSecrets Secrets)> injecting,
        CancellationToken cancellationToken)
    {
        var missing = new List<string>();
        foreach (var (key, sandbox, secrets) in injecting)
            missing.AddRange(
                (await probe.MissingAsync(sandbox, secrets, cancellationToken))
                .Select(name => $"{key}: {name}"));

        var declared = injecting.Sum(i => i.Secrets.Env.Count + i.Secrets.Files.Count);
        return missing.Count == 0
            ? RunPreflightFinding.Pass(
                Name, $"{declared} injected credential(s) present in {injecting.Count} sandbox(es)")
            : RunPreflightFinding.Fail(
                Name,
                "declared credential(s) did not arrive in the sandbox: " + string.Join(", ", missing),
                Lever);
    }

    private static string NotInjected(int declared, int sandboxes) =>
        $"{declared} credential(s) are declared, and none of this run's {sandboxes} sandbox(es) "
        + "injects secrets — the credentials are NOT INJECTED on this backend rather than "
        + "missing from the repository, and a step that needs one will say so itself";

    private static IReadOnlyDictionary<string, ISandbox> Sandboxes(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes) && sandboxes is not null
            ? sandboxes
            : new Dictionary<string, ISandbox>(StringComparer.Ordinal);

    private static IReadOnlyList<(string Key, ISandbox Sandbox, ResolvedSandboxSecrets Secrets)>
        Injecting(IReadOnlyDictionary<string, ISandbox> sandboxes) =>
        [.. sandboxes
            .Where(s => s.Value is ISandboxSecretInjection)
            .Select(s => (s.Key, s.Value, ((ISandboxSecretInjection)s.Value).InjectedSecrets))
            .Where(s => s.Item3.Env.Count + s.Item3.Files.Count > 0)];

    private static int DeclaredCount(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)) return 0;
        var declared = project?.Sandbox?.Secrets;
        return (declared?.Env?.Count ?? 0) + (declared?.Files?.Count ?? 0);
    }
}
