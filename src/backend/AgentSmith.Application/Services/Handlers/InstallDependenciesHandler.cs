using CommandLineStringSplitter = System.CommandLine.Parsing.CommandLineStringSplitter;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0202 + p0202a: runs each context's <c>ci.install_command</c> in its sandbox
/// so non-dotnet test runners (jest, pytest, mvn, cargo, go) find dependencies
/// installed before the Test step. The command is the operator-owned, durable
/// value read from context.yaml at discovery time (RemoteContextDiscovery.
/// InstallCommand) — available this early in the pipeline, unlike the analyzer's
/// ProjectMap which AnalyzeCode produces much later. A context with no install
/// command logs a skip and continues (docs-only / no-deps repos must not fail).
/// Per-repo non-zero exits aggregate into a single failure naming the offenders.
/// </summary>
public sealed class InstallDependenciesHandler(
    ILogger<InstallDependenciesHandler> logger)
    : ICommandHandler<InstallDependenciesContext>
{
    // Aligns with TestHandler: the SandboxGlobalConfig.StepTimeoutSeconds cap
    // (p0200) is applied by the sandbox backend; passing the cap value keeps the
    // operator's agentsmith.yml `sandbox.step_timeout_seconds` the single knob.
    private const int InstallTimeoutSeconds = 900;

    public async Task<CommandResult> ExecuteAsync(
        InstallDependenciesContext context, CancellationToken cancellationToken)
    {
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No sandboxes/discoveries in pipeline context; skipping install.");

        var outcomes = new List<InstallOutcome>(sandboxes.Count);
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var discovery))
                continue;
            var command = discovery.InstallCommand;
            if (string.IsNullOrWhiteSpace(command))
            {
                logger.LogInformation("{Key}: no ci.install_command in context.yaml — skipping", key);
                outcomes.Add(new InstallOutcome(key, ExitCode: 0, Skipped: true));
                continue;
            }
            var workdir = SubTreeWorkdir(discovery.Workdir);
            outcomes.Add(await RunOneAsync(key, sandbox, workdir, command!, cancellationToken));
        }

        return BuildAggregateResult(outcomes);
    }

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

    private CommandResult BuildAggregateResult(IReadOnlyList<InstallOutcome> outcomes)
    {
        var ran = outcomes.Where(o => !o.Skipped).ToList();
        if (ran.Count == 0) return CommandResult.Ok("No contexts had a ci.install_command; skipped all.");

        var failed = ran.Where(o => o.ExitCode != 0).ToList();
        if (failed.Count == 0)
            return CommandResult.Ok($"Dependencies installed ({ran.Count} repo(s))");

        var names = string.Join(", ", failed.Select(o => string.IsNullOrEmpty(o.Key) ? "(default)" : o.Key));
        var detail = Tail(failed[0].Output, 800);
        var reason = string.IsNullOrWhiteSpace(detail) ? "" : $"\n{failed[0].Key}: {detail}";
        return CommandResult.Fail(
            $"Dependency install failed in {failed.Count}/{ran.Count} repo(s): {names}{reason}");
    }

    private static string SubTreeWorkdir(string workdir) =>
        workdir == "." ? Repository.SandboxWorkPath : $"{Repository.SandboxWorkPath}/{workdir}";

    private static string Combine(string? stdout, string? stderr) =>
        string.Join("\n", new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    private static string Tail(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : "…" + text[^max..];

    private sealed record InstallOutcome(string Key, int ExitCode, bool Skipped, string? Output = null);
}
