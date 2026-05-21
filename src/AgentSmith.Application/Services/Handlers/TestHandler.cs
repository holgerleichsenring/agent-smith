using System.Text.Json;
using CommandLineStringSplitter = System.CommandLine.Parsing.CommandLineStringSplitter;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Runs per-repo tests against the code changes (p0158f). Iterates
/// ContextKeys.RepoProjectMaps; each repo with a non-empty ci.test_command
/// runs in its OWN per-repo sandbox (Sandboxes[repo.Name]) where /work is
/// the repo root. Repos with empty test_command are skipped (docs/markdown
/// repos shouldn't fail the test gate). Aggregates TrxSummary via
/// Combine; overall pass/fail is logical-AND.
/// </summary>
public sealed class TestHandler(
    TrxResultParser trxParser,
    ILogger<TestHandler> logger)
    : ICommandHandler<TestContext>
{
    private const int TestTimeoutSeconds = 300;
    private const string TrxResultsDir = "/work/test-results";

    public async Task<CommandResult> ExecuteAsync(
        TestContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running tests for {Changes} changes...", context.Changes.Count);

        var perRepo = ResolvePerRepoMaps(context.Pipeline);
        if (perRepo is null || perRepo.Count == 0)
        {
            context.Pipeline.Set(ContextKeys.TestResults, TrxSummary.Empty);
            return CommandResult.Ok("Skipping tests: ProjectMap missing — AnalyzeProject did not run.");
        }
        var (sandboxes, _) = MultiRepoTargets.Resolve(context.Pipeline);
        if (sandboxes is null) return SkipWithoutSandbox(context);

        var outcomes = new List<RepoTestOutcome>(perRepo.Count);
        var aggregate = TrxSummary.Empty;
        foreach (var (repoName, map) in perRepo)
        {
            var resolved = ResolveTestCommand(map);
            if (resolved.Command is null)
            {
                logger.LogInformation("{Repo}: skip — {Reason}", repoName, resolved.SkipReason);
                outcomes.Add(new RepoTestOutcome(repoName, ExitCode: 0, Summary: TrxSummary.Empty, Skipped: true));
                continue;
            }
            if (!sandboxes.TryGetValue(repoName, out var sandbox))
            {
                logger.LogWarning("{Repo}: no sandbox available; skipping", repoName);
                continue;
            }
            var outcome = await RunOneAsync(repoName, sandbox, resolved, cancellationToken);
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

    // Multi-repo path uses ContextKeys.RepoProjectMaps; single-repo back-compat
    // synthesizes a one-entry dict from ContextKeys.ProjectMap.
    private static IReadOnlyDictionary<string, ProjectMap>? ResolvePerRepoMaps(PipelineContext pipeline)
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

    private async Task<RepoTestOutcome> RunOneAsync(
        string repoName, ISandbox sandbox, TestCommand cmd, CancellationToken ct)
    {
        logger.LogInformation("{Repo}: running {Command} {Args}",
            repoName, cmd.Command, string.Join(' ', cmd.Args ?? Array.Empty<string>()));
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: cmd.Command, Args: cmd.Args, TimeoutSeconds: TestTimeoutSeconds);
        var result = await sandbox.RunStepAsync(step, progress: null, ct);

        if (!cmd.IsTrxCapable)
            return new RepoTestOutcome(repoName, result.ExitCode, TrxSummary.Empty, Skipped: false);

        var summary = await CollectTrxAsync(sandbox, ct);
        return new RepoTestOutcome(repoName, result.ExitCode, summary, Skipped: false);
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
        IReadOnlyList<RepoTestOutcome> outcomes, TrxSummary aggregate)
    {
        var ran = outcomes.Where(o => !o.Skipped).ToList();
        if (ran.Count == 0) return CommandResult.Ok("No repos had a test command; skipped all.");

        var failedRepos = ran.Where(o => o.ExitCode != 0 || o.Summary.FailedCount > 0).ToList();
        var counters = $"{aggregate.PassedCount}/{aggregate.TotalCount} passed, {aggregate.FailedCount} failed";
        if (failedRepos.Count == 0)
            return CommandResult.Ok($"Tests passed ({counters})");

        var failureLines = aggregate.Failures.Select(f => $"  - {f.TestName}: {f.ErrorMessage}");
        var detail = string.Join('\n', failureLines);
        var exitSummary = failedRepos[0].ExitCode;
        return CommandResult.Fail($"Tests failed ({counters}, exit {exitSummary}):\n{detail}");
    }

    internal sealed record TestCommand(string? Command, IReadOnlyList<string>? Args, bool IsTrxCapable, string? SkipReason)
    {
        public static TestCommand Skip(string reason) => new(null, null, false, reason);
    }

    private sealed record RepoTestOutcome(string RepoName, int ExitCode, TrxSummary Summary, bool Skipped);
}
