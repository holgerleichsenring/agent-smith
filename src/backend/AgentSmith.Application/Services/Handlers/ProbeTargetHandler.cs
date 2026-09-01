using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

using Declaration = (string Key, AgentSmith.Contracts.Sandbox.ISandbox Sandbox,
    AgentSmith.Application.Services.Handlers.ContextTargetProbe Probe);

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-01-379a: asks every declared target whether it answers, before the master spends
/// a token, and fails the run when one refuses.
/// <para>
/// A command that resolves authentication before it validates reds on a clean tree without
/// credentials — a fact about the measurement environment, not about the command. With the
/// credential it is a real gate, and this is the step that buys it early: a wrong or absent
/// credential surfaces as infrastructure here instead of as a coding agent failing to build.
/// </para>
/// </summary>
public sealed class ProbeTargetHandler(
    ContextTargetProbeResolver probes,
    TargetProbeRunner runner,
    ILogger<ProbeTargetHandler> logger)
    : ICommandHandler<ProbeTargetContext>
{
    public async Task<CommandResult> ExecuteAsync(
        ProbeTargetContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var declared = Declared(context.Pipeline);
        if (declared.Count == 0)
        {
            logger.LogInformation("No context declares a target probe — nothing to ask");
            return CommandResult.Ok(TargetProbeReport.NotDeclared);
        }

        // Only the Kubernetes pod builder consumes the run's declared secrets; the docker and
        // in-process sandboxes carry none. Asking there would redden every harness run of a
        // repository that declares a probe, for a reason that is not about the repository.
        var asked = declared.Where(target => Injects(target.Sandbox)).ToList();
        var skipped = declared.Where(target => !Injects(target.Sandbox)).ToList();
        return await AskAsync(asked, skipped, cancellationToken);
    }

    private async Task<CommandResult> AskAsync(
        IReadOnlyList<Declaration> asked, IReadOnlyList<Declaration> skipped,
        CancellationToken cancellationToken)
    {
        foreach (var (key, sandbox, probe) in asked)
        {
            var exitCode = await runner.AskAsync(key, sandbox, probe, cancellationToken);
            if (exitCode != 0) return CommandResult.Fail(TargetProbeReport.Refused(key, probe, exitCode));
        }

        var answered = asked.Select(target => target.Probe).ToList();
        if (skipped.Count == 0) return CommandResult.Ok(TargetProbeReport.Answered(answered));

        logger.LogWarning(
            "{Count} declared target probe(s) were not asked: this backend injects no credentials",
            skipped.Count);
        return CommandResult.Ok(TargetProbeReport.Skipped(
            [.. skipped.Select(target => target.Probe)], answered.Count));
    }

    private IReadOnlyList<Declaration> Declared(PipelineContext pipeline) =>
        [.. Sandboxes(pipeline).SelectMany(sandbox => probes
            .For(pipeline, sandbox.Key)
            .Select(probe => (Key: sandbox.Key, Sandbox: sandbox.Value, Probe: probe)))];

    private static bool Injects(ISandbox sandbox) =>
        sandbox is ISandboxSecretInjection injection
        && injection.InjectedSecrets.Env.Count + injection.InjectedSecrets.Files.Count > 0;

    private static IReadOnlyDictionary<string, ISandbox> Sandboxes(PipelineContext pipeline) =>
        pipeline.TryGet<IReadOnlyDictionary<string, ISandbox>>(
            ContextKeys.Sandboxes, out var sandboxes) && sandboxes is not null
            ? sandboxes
            : new Dictionary<string, ISandbox>(StringComparer.Ordinal);
}
