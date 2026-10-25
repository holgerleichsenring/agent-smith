using CommandLineStringSplitter = System.CommandLine.Parsing.CommandLineStringSplitter;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0202e: prepares each context's environment before tests by running its
/// prerequisite command (npm install, pip install, go mod download, …) in the
/// sandbox. The command is the analyzer-DERIVED, repo-state-aware
/// ProjectMap.Prerequisites by default; an operator context.yaml
/// `prerequisites` (RemoteContextDiscovery.Prerequisites) overrides it — needed
/// for analyzer-less presets (e.g. legal-analysis' markitdown). A context with
/// no command logs a skip (docs-only / no-deps / .NET must not fail). Per-repo
/// non-zero exits aggregate into a single failure naming the offenders, with
/// the failing command's output captured for diagnosis.
/// </summary>
public sealed class EnsurePrerequisitesHandler(
    SandboxTargets sandboxTargets,
    ILogger<EnsurePrerequisitesHandler> logger)
    : ICommandHandler<EnsurePrerequisitesContext>
{
    // The backend applies sandbox.step_timeout_seconds (p0200), the single knob that changes this.
    private const int InstallTimeoutSeconds = 900;

    // 2026-10-25-c5a7a: a prerequisite that apt-installs fails under the hardened default; say why.
    private const string HardenedHint =
        "\nOn Docker and Kubernetes the sandbox runs as uid 1000, not root. A prerequisite that needs "
        + "root (apt-get, a global npm install) runs once the project sets sandbox.run_as_root: true.";

    public async Task<CommandResult> ExecuteAsync(
        EnsurePrerequisitesContext context, CancellationToken cancellationToken)
    {
        if (!sandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No sandboxes/discoveries in pipeline context; skipping install.");

        // Only the operator-declared context.yaml prerequisite runs: a derived install guessed
        // command and subtree and failed on real layouts, so dependency installs are the coding
        // master's job. It runs at the REPOSITORY ROOT; a command needing another directory
        // carries its own cd, because meta.workdir says where source lives, not the manifest.
        var outcomes = new List<InstallOutcome>(sandboxes.Count);
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var discovery))
                continue;
            var command = discovery.Prerequisites;
            if (string.IsNullOrWhiteSpace(command))
            {
                logger.LogInformation("{Key}: no context.yaml prerequisites — the coding agent installs deps as needed; skipping", key);
                outcomes.Add(new InstallOutcome(key, ExitCode: 0, Skipped: true));
                continue;
            }
            outcomes.Add(await RunOneAsync(
                key, sandbox, Repository.SandboxWorkPath, command!, cancellationToken));
        }

        return BuildAggregateResult(outcomes, RunsHardened(context.Pipeline));
    }

    private static bool RunsHardened(PipelineContext pipeline) =>
        !(pipeline.TryGet<ResolvedProject>(ContextKeys.ProjectConfig, out var project)
          && project?.Sandbox?.RunAsRoot == true);

    private async Task<InstallOutcome> RunOneAsync(
        string key, ISandbox sandbox, string workingDirectory, string rawCommand, CancellationToken ct)
    {
        var tokens = CommandLineStringSplitter.Instance.Split(rawCommand).ToList();
        if (tokens.Count == 0)
        {
            logger.LogWarning("{Key}: install command '{Raw}' tokenized to zero arguments; skipping", key, rawCommand);
            return new InstallOutcome(key, ExitCode: 0, Skipped: true);
        }

        logger.LogInformation("{Key}: installing dependencies via {Command} at {Cwd}", key, rawCommand, workingDirectory);
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: tokens[0], Args: tokens.Skip(1).ToArray(),
            WorkingDirectory: workingDirectory, TimeoutSeconds: InstallTimeoutSeconds);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        var output = Combine(result.OutputContent, result.ErrorMessage);
        if (result.ExitCode != 0)
            // Surface WHY: without this the operator only sees "failed in repo X"
            // and the dashboard shows "waiting for stdout…" — undiagnosable.
            logger.LogError("{Key}: '{Command}' failed (exit {Exit}) at {Cwd}:\n{Output}",
                key, rawCommand, result.ExitCode, workingDirectory, Tail(output, 4000));
        return new InstallOutcome(key, result.ExitCode, Skipped: false, Output: output);
    }

    private CommandResult BuildAggregateResult(IReadOnlyList<InstallOutcome> outcomes, bool hardened)
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

    private static string Combine(string? stdout, string? stderr) =>
        string.Join("\n", new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    private static string Tail(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : "…" + text[^max..];

    private sealed record InstallOutcome(string Key, int ExitCode, bool Skipped, string? Output = null);
}
