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
/// p0202: runs each context's <c>ci.install_command</c> in its own sandbox so
/// non-dotnet test runners (jest, pytest, mvn, cargo, go) find dependencies
/// installed before the Test step. Iterates ContextKeys.Sandboxes the same way
/// TestHandler does; per context with an empty install command logs a skip and
/// continues (docs-only / no-deps repos must not fail). Per-repo non-zero exits
/// aggregate into a single failure naming the offending repos.
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
        var perKey = ResolvePerKeyMaps(context.Pipeline);
        if (perKey is null || perKey.Count == 0)
            return CommandResult.Ok("Skipping install: ProjectMap missing — AnalyzeProject did not run.");
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Fail(
                "Dependency install requires an active sandbox; none is present in the pipeline context. " +
                "Check the Server-Pod's startup logs for sandbox-creation errors.");

        var outcomes = new List<InstallOutcome>(perKey.Count);
        foreach (var (key, map) in perKey)
        {
            var command = map.Ci?.InstallCommand;
            if (string.IsNullOrWhiteSpace(command))
            {
                logger.LogInformation("{Key}: no install command in context.yaml — skipping", key);
                outcomes.Add(new InstallOutcome(key, ExitCode: 0, Skipped: true));
                continue;
            }
            if (!sandboxes.TryGetValue(key, out var sandbox))
            {
                logger.LogWarning("{Key}: no sandbox available; skipping", key);
                continue;
            }
            var workdir = discoveries.TryGetValue(key, out var discovery)
                ? SubTreeWorkdir(discovery.Workdir)
                : Repository.SandboxWorkPath;
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
        return new InstallOutcome(key, result.ExitCode, Skipped: false);
    }

    private CommandResult BuildAggregateResult(IReadOnlyList<InstallOutcome> outcomes)
    {
        var ran = outcomes.Where(o => !o.Skipped).ToList();
        if (ran.Count == 0) return CommandResult.Ok("No contexts had an install command; skipped all.");

        var failed = ran.Where(o => o.ExitCode != 0).ToList();
        if (failed.Count == 0)
            return CommandResult.Ok($"Dependencies installed ({ran.Count} repo(s))");

        var names = string.Join(", ", failed.Select(o => string.IsNullOrEmpty(o.Key) ? "(default)" : o.Key));
        return CommandResult.Fail($"Dependency install failed in {failed.Count}/{ran.Count} repo(s): {names}");
    }

    private static string SubTreeWorkdir(string workdir) =>
        workdir == "." ? Repository.SandboxWorkPath : $"{Repository.SandboxWorkPath}/{workdir}";

    // Mirrors TestHandler's private resolver — kept local (not shared) so the
    // two handlers stay independent; the logic is small and identical in shape.
    private static IReadOnlyDictionary<string, ProjectMap>? ResolvePerKeyMaps(PipelineContext pipeline)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
                ContextKeys.RepoProjectMaps, out var dict) && dict is { Count: > 0 })
            return dict;
        if (pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var single) && single is not null)
            return new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [string.Empty] = single };
        return null;
    }

    private sealed record InstallOutcome(string Key, int ExitCode, bool Skipped);
}
