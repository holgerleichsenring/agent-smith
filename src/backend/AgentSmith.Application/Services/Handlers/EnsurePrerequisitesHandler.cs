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
    ILogger<EnsurePrerequisitesHandler> logger)
    : ICommandHandler<EnsurePrerequisitesContext>
{
    // The SandboxGlobalConfig.StepTimeoutSeconds cap (p0200) is applied by the
    // sandbox backend; passing the cap value keeps the operator's agentsmith.yml
    // `sandbox.step_timeout_seconds` the single knob that changes behaviour.
    private const int InstallTimeoutSeconds = 900;

    public async Task<CommandResult> ExecuteAsync(
        EnsurePrerequisitesContext context, CancellationToken cancellationToken)
    {
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return CommandResult.Ok("No sandboxes/discoveries in pipeline context; skipping install.");

        // p0202e: the command source is the operator override from context.yaml
        // (discovery.Prerequisites) if set, ELSE the analyzer-derived,
        // repo-state-aware value from the ProjectMap (AnalyzeCode). The override
        // covers cases the analyzer can't derive (pipeline tools like markitdown
        // in analyzer-less presets); derivation is the default for code projects.
        var maps = ResolvePerKeyMaps(context.Pipeline);

        var outcomes = new List<InstallOutcome>(sandboxes.Count);
        foreach (var (key, sandbox) in sandboxes)
        {
            if (!discoveries.TryGetValue(key, out var discovery))
                continue;
            var map = maps is not null && maps.TryGetValue(key, out var resolvedMap) ? resolvedMap : null;
            var command = !string.IsNullOrWhiteSpace(discovery.Prerequisites)
                ? discovery.Prerequisites
                : map?.Prerequisites;
            if (string.IsNullOrWhiteSpace(command))
            {
                logger.LogInformation("{Key}: no operator override and no analyzer-derived initialize command — skipping", key);
                outcomes.Add(new InstallOutcome(key, ExitCode: 0, Skipped: true));
                continue;
            }
            // p0212: run the command where the project actually lives. The
            // operator's meta.workdir override wins; else the analyzer's module
            // paths derive the project subtree (e.g. Sample.Client/ for an npm
            // project), so `npm install` finds package.json instead of ENOENT
            // at the repo root.
            var workdir = SubTreeWorkdir(CommandWorkingDirectory.Resolve(map, discovery.Workdir));
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
        if (ran.Count == 0) return CommandResult.Ok("No contexts had an initialize command; skipped all.");

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

    // p0202e: per-context analyzer ProjectMaps (set by AnalyzeCode). Null when
    // the preset has no AnalyzeCode (e.g. legal-analysis) — then only the
    // context.yaml override applies.
    private static IReadOnlyDictionary<string, ProjectMap>? ResolvePerKeyMaps(PipelineContext pipeline)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
                ContextKeys.RepoProjectMaps, out var dict) && dict is { Count: > 0 })
            return dict;
        if (pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var single) && single is not null)
            return new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [string.Empty] = single };
        return null;
    }

    private static string Combine(string? stdout, string? stderr) =>
        string.Join("\n", new[] { stdout, stderr }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();

    private static string Tail(string? text, int max) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : "…" + text[^max..];

    private sealed record InstallOutcome(string Key, int ExitCode, bool Skipped, string? Output = null);
}
