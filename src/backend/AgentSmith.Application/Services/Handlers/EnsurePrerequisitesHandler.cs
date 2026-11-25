using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Options;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0202e: prepares each context's environment before tests by running its context.yaml
/// `prerequisites` command (npm install, pip install, …) in the sandbox. A context with no
/// command logs a skip (docs-only / no-deps / .NET must not fail). Per-repo non-zero exits
/// aggregate into a single failure naming the offenders, with the failing command's output.
/// </summary>
public sealed class EnsurePrerequisitesHandler(
    SandboxTargets sandboxTargets,
    IOptions<SandboxGlobalConfig> sandboxConfig,
    PrerequisiteInstall install)
    : ICommandHandler<EnsurePrerequisitesContext>
{
    // 2026-10-25-c5a7a/ff3b: a prerequisite that apt-installs fails in a hardened sandbox; say why.
    private const string HardenedHint =
        "\nThis installation runs sandboxes as uid 1000, not root (sandbox.hardening_enabled). A prerequisite "
        + "that needs root (apt-get, a global npm install) runs once the project sets sandbox.run_as_root: true.";

    public async Task<CommandResult> ExecuteAsync(
        EnsurePrerequisitesContext context, CancellationToken cancellationToken)
    {
        if (!sandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No sandboxes/discoveries in pipeline context; skipping install.");

        // Only the operator-declared context.yaml prerequisite runs: a derived install guessed
        // command and subtree and failed on real layouts, so dependency installs are the coding
        // master's job.
        var outcomes = new List<PrerequisiteOutcome>(sandboxes.Count);
        foreach (var (key, sandbox) in sandboxes)
            if (discoveries.TryGetValue(key, out var discovery))
                outcomes.Add(await install.RunAsync(key, sandbox, discovery.Prerequisites, cancellationToken));

        return BuildAggregateResult(outcomes, RunsHardened(context.Pipeline));
    }

    private bool RunsHardened(PipelineContext pipeline) => SandboxSecurityContext.For(
        sandboxConfig.Value.HardeningEnabled,
        pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)
        && project?.Sandbox?.RunAsRoot == true) == SandboxSecurityContext.Hardened;

    private static CommandResult BuildAggregateResult(IReadOnlyList<PrerequisiteOutcome> outcomes, bool hardened)
    {
        var ran = outcomes.Where(o => !o.Skipped).ToList();
        if (ran.Count == 0) return CommandResult.Ok("No contexts had an initialize command; skipped all.");

        var failed = ran.Where(o => o.ExitCode != 0).ToList();
        if (failed.Count == 0)
            return CommandResult.Ok($"Dependencies installed ({ran.Count} repo(s))");

        var names = string.Join(", ", failed.Select(o => string.IsNullOrEmpty(o.Key) ? "(default)" : o.Key));
        var detail = Tail(failed[0].Output, 800);
        var reason = string.IsNullOrWhiteSpace(detail) ? "" : $"\n{failed[0].Key}: {detail}";
        return CommandResult.Fail(
            $"Dependency install failed in {failed.Count}/{ran.Count} repo(s): {names}{reason}"
            + (hardened ? HardenedHint : ""));
    }

    private static string Tail(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : "…" + text[^max..];
}
