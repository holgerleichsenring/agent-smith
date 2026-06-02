using System.Text.Json;
using CommandLineStringSplitter = System.CommandLine.Parsing.CommandLineStringSplitter;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Runs per-context tests against the code changes (p0158f + p0161a).
/// Iterates ContextKeys.Sandboxes keys; each key with a non-empty
/// ci.test_command runs in its OWN sandbox at
/// WorkingDirectory = /work/{discovery.Workdir} (operative root for monorepo
/// sub-trees; /work for single-stack repos). Contexts with empty
/// test_command are skipped. TrxSummary aggregation is across all per-key
/// runs.
/// </summary>
public sealed class TestHandler(
    TrxResultParser trxParser,
    ILogger<TestHandler> logger)
    : ICommandHandler<TestContext>
{
    // 900s aligns with the SandboxGlobalConfig.StepTimeoutSeconds default
    // cap (p0200) — keeping the handler's own value at the cap means the
    // operator's agentsmith.yml `sandbox.step_timeout_seconds` override is
    // the single knob that actually changes behaviour.
    private const int TestTimeoutSeconds = 900;
    private const string TrxResultsDir = "/work/test-results";

    public async Task<CommandResult> ExecuteAsync(
        TestContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running tests for {Changes} changes...", context.Changes.Count);

        var perKey = ResolvePerKeyMaps(context.Pipeline);
        if (perKey is null || perKey.Count == 0)
        {
            context.Pipeline.Set(ContextKeys.TestResults, TrxSummary.Empty);
            return CommandResult.Ok("Skipping tests: ProjectMap missing — AnalyzeProject did not run.");
        }
        if (!SandboxTargets.TryResolve(context.Pipeline, out var sandboxes, out var discoveries))
            return SkipWithoutSandbox(context);

        var outcomes = new List<TestOutcome>(perKey.Count);
        var aggregate = TrxSummary.Empty;
        foreach (var (key, map) in perKey)
        {
            var resolved = ResolveTestCommand(map);
            if (resolved.Command is null)
            {
                logger.LogInformation("{Key}: skip — {Reason}", key, resolved.SkipReason);
                outcomes.Add(new TestOutcome(key, ExitCode: 0, Summary: TrxSummary.Empty, Skipped: true));
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
            var outcome = await RunOneAsync(key, sandbox, workdir, resolved, cancellationToken);
            outcomes.Add(outcome);
            aggregate = aggregate.Combine(outcome.Summary);
        }

        context.Pipeline.Set(ContextKeys.TestResults, aggregate);
        return BuildAggregateResult(outcomes, aggregate);
    }

    private CommandResult SkipWithoutSandbox(TestContext context)
    {
        logger.LogError("No sandbox in pipeline context — failing test step");
        context.Pipeline.Set(ContextKeys.TestResults, TrxSummary.Empty);
        return CommandResult.Fail(
            "Test execution requires an active sandbox; none is present in the pipeline context. " +
            "This usually means the sandbox factory failed to create a runtime (RBAC, image pull, " +
            "or local Docker socket). Check the Server-Pod's startup logs for sandbox-creation errors.");
    }

    private static string SubTreeWorkdir(string workdir) =>
        workdir == "." ? Repository.SandboxWorkPath : $"{Repository.SandboxWorkPath}/{workdir}";

    private static IReadOnlyDictionary<string, ProjectMap>? ResolvePerKeyMaps(PipelineContext pipeline)
    {
        if (pipeline.TryGet<IReadOnlyDictionary<string, ProjectMap>>(
                ContextKeys.RepoProjectMaps, out var dict) && dict is { Count: > 0 })
            return dict;
        if (pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var single) && single is not null)
            return new Dictionary<string, ProjectMap>(StringComparer.Ordinal) { [string.Empty] = single };
        return null;
    }

    // Back-compat overload for fixture tests that seed only ContextKeys.ProjectMap.
    internal static TestCommand ResolveTestCommand(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var map) || map is null)
            return TestCommand.Skip("Skipping tests: ProjectMap missing — AnalyzeProject did not run.");
        return ResolveTestCommand(map);
    }

    internal static TestCommand ResolveTestCommand(ProjectMap projectMap)
    {
        var raw = projectMap.Ci?.TestCommand;
        if (string.IsNullOrWhiteSpace(raw))
            return TestCommand.Skip("Skipping tests: ProjectMap.Ci.TestCommand is empty — analyzer found no test command for this project.");

        var tokens = CommandLineStringSplitter.Instance.Split(raw).ToList();
        if (tokens.Count == 0)
            return TestCommand.Skip($"ci.test_command '{raw}' tokenized to zero arguments.");

        var head = tokens[0];
        var isDotnet = head.Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        if (isDotnet)
            tokens.AddRange(new[] { "--logger", "trx", "--results-directory", TrxResultsDir });

        return new TestCommand(head, tokens.Skip(1).ToArray(), IsTrxCapable: isDotnet, SkipReason: null);
    }

    private async Task<TestOutcome> RunOneAsync(
        string key, ISandbox sandbox, string workingDirectory, TestCommand cmd, CancellationToken ct)
    {
        logger.LogInformation("{Key}: running {Command} {Args} at {Cwd}",
            key, cmd.Command, string.Join(' ', cmd.Args ?? Array.Empty<string>()), workingDirectory);
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: cmd.Command, Args: cmd.Args, WorkingDirectory: workingDirectory,
            TimeoutSeconds: TestTimeoutSeconds);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);

        if (!cmd.IsTrxCapable)
            return new TestOutcome(key, result.ExitCode, TrxSummary.Empty, Skipped: false);

        var summary = await CollectTrxAsync(sandbox, ct);
        return new TestOutcome(key, result.ExitCode, summary, Skipped: false);
    }

    private async Task<TrxSummary> CollectTrxAsync(ISandbox sandbox, CancellationToken ct)
    {
        var listStep = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            Path: TrxResultsDir, MaxDepth: 4);
        var listResult = await sandbox.RunStepAsync(listStep, progress: null, ct);
        if (listResult.ExitCode != 0 || string.IsNullOrEmpty(listResult.OutputContent))
            return TrxSummary.Empty;

        var trxPaths = ExtractPathsEndingWith(listResult.OutputContent, ".trx");
        var summary = TrxSummary.Empty;
        foreach (var trxPath in trxPaths)
            summary = summary.Combine(await ParseSingleAsync(sandbox, trxPath, ct));
        return summary;
    }

    private static IReadOnlyList<string> ExtractPathsEndingWith(string json, string suffix)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        var paths = new List<string>();
        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var value = entry.ValueKind switch
            {
                JsonValueKind.String => entry.GetString(),
                JsonValueKind.Object when entry.TryGetProperty("path", out var p) => p.GetString(),
                _ => null
            };
            if (!string.IsNullOrEmpty(value) && value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                paths.Add(value);
        }
        return paths;
    }

    private async Task<TrxSummary> ParseSingleAsync(ISandbox sandbox, string path, CancellationToken ct)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, Path: path);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);
        return result.ExitCode == 0 && result.OutputContent is not null
            ? trxParser.Parse(result.OutputContent)
            : TrxSummary.Empty;
    }

    private static CommandResult BuildAggregateResult(
        IReadOnlyList<TestOutcome> outcomes, TrxSummary aggregate)
    {
        var ran = outcomes.Where(o => !o.Skipped).ToList();
        if (ran.Count == 0) return CommandResult.Ok("No contexts had a test command; skipped all.");

        // p0202: a per-repo run with exit 0 AND zero discovered tests is a
        // distinct NoTests state, not a failure — catches dotnet (TRX-empty,
        // exit 0) and jest (no TRX written, exit 0 when zero tests). pytest's
        // exit-5-on-zero-collected stays Fail (documented out: in p0202).
        var failed = ran.Where(o => Classify(o) == OutcomeStatus.Fail).ToList();
        var noTests = ran.Count(o => Classify(o) == OutcomeStatus.NoTests);
        var passed = ran.Count - failed.Count - noTests;
        var repoCounts = $"{passed} passed / {noTests} no-tests / {failed.Count} failed";
        var caseCounts = $"{aggregate.PassedCount}/{aggregate.TotalCount} test cases, {aggregate.FailedCount} failed";

        if (failed.Count == 0)
            return CommandResult.Ok($"Tests OK ({repoCounts}; {caseCounts})");

        var failureLines = aggregate.Failures.Select(f => $"  - {f.TestName}: {f.ErrorMessage}");
        var detail = string.Join('\n', failureLines);
        var exitSummary = failed[0].ExitCode;
        return CommandResult.Fail($"Tests failed ({repoCounts}; {caseCounts}, exit {exitSummary}):\n{detail}");
    }

    private static OutcomeStatus Classify(TestOutcome outcome) =>
        outcome.ExitCode != 0 || outcome.Summary.FailedCount > 0 ? OutcomeStatus.Fail
        : outcome.Summary.TotalCount == 0 ? OutcomeStatus.NoTests
        : OutcomeStatus.Pass;

    internal sealed record TestCommand(string? Command, IReadOnlyList<string>? Args, bool IsTrxCapable, string? SkipReason)
    {
        public static TestCommand Skip(string reason) => new(null, null, false, reason);
    }

    private enum OutcomeStatus { Pass, Fail, NoTests }

    private sealed record TestOutcome(string Key, int ExitCode, TrxSummary Summary, bool Skipped);
}
