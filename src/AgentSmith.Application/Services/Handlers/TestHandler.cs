using System.Text.Json;
using CommandLineStringSplitter = System.CommandLine.Parsing.CommandLineStringSplitter;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Runs project-specific tests against the code changes. Routes through ISandbox
/// when available; falls back to detected-project test command for CLI mode.
/// For dotnet projects, captures structured TRX results into ContextKeys.TestResults.
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

        var resolved = ResolveTestCommand(context.Pipeline);
        if (resolved.Command is null)
        {
            var reason = resolved.SkipReason ?? "no test command resolved";
            logger.LogWarning("{Reason}", reason);
            context.Pipeline.Set(ContextKeys.TestResults, TrxSummary.Empty);
            return CommandResult.Ok(reason);
        }

        if (!context.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return SkipWithoutSandbox(context);

        return await RunInSandboxAsync(context, sandbox, resolved, cancellationToken);
    }

    // p0155: TestHandler runs whatever ci.test_command the analyzer produced,
    // tokenized via System.CommandLine's established splitter (no hand-rolled
    // whitespace + quote handling). IsTrxCapable derives from the command
    // prefix — a property of the runner, not the project's language.
    internal static TestCommand ResolveTestCommand(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var projectMap) || projectMap is null)
            return TestCommand.Skip("Skipping tests: ProjectMap missing — AnalyzeProject did not run.");

        var raw = projectMap.Ci?.TestCommand;
        if (string.IsNullOrWhiteSpace(raw))
            return TestCommand.Skip(
                "Skipping tests: ProjectMap.Ci.TestCommand is empty — analyzer found no test command for this project.");

        var tokens = CommandLineStringSplitter.Instance.Split(raw).ToList();
        if (tokens.Count == 0)
            return TestCommand.Skip($"Skipping tests: ci.test_command '{raw}' tokenized to zero arguments.");

        // dotnet test emits TRX when --logger:trx + --results-directory is appended.
        // Append rather than overwrite so analyzer-supplied flags (e.g. --filter) survive.
        var head = tokens[0];
        var isDotnet = head.Equals("dotnet", StringComparison.OrdinalIgnoreCase);
        if (isDotnet)
            tokens.AddRange(new[] { "--logger", "trx", "--results-directory", TrxResultsDir });

        return new TestCommand(head, tokens.Skip(1).ToArray(), IsTrxCapable: isDotnet, SkipReason: null);
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

    private async Task<CommandResult> RunInSandboxAsync(
        TestContext context, ISandbox sandbox, TestCommand cmd, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running in sandbox: {Command} {Args}", cmd.Command, string.Join(' ', cmd.Args ?? Array.Empty<string>()));
        var output = await ExecuteTestStepAsync(sandbox, cmd, cancellationToken);

        if (!cmd.IsTrxCapable)
        {
            context.Pipeline.Set(ContextKeys.TestResults, output.Result);
            return output.ExitCode == 0
                ? CommandResult.Ok("Tests passed")
                : CommandResult.Fail($"Tests failed (exit code {output.ExitCode}):\n{output.Result}");
        }

        var summary = await CollectTrxAsync(sandbox, cancellationToken);
        context.Pipeline.Set(ContextKeys.TestResults, summary);
        return BuildResult(summary, output.ExitCode, output.Result);
    }

    private static async Task<(int ExitCode, string Result)> ExecuteTestStepAsync(
        ISandbox sandbox, TestCommand cmd, CancellationToken cancellationToken)
    {
        var stdout = new System.Text.StringBuilder();
        var progress = new Progress<StepEvent>(ev =>
        {
            if (ev.Kind is StepEventKind.Stdout or StepEventKind.Stderr)
                stdout.AppendLine(ev.Line);
        });
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: cmd.Command, Args: cmd.Args, TimeoutSeconds: TestTimeoutSeconds);
        var result = await sandbox.RunStepAsync(step, progress, cancellationToken);
        return (result.ExitCode, stdout.ToString());
    }

    private async Task<TrxSummary> CollectTrxAsync(ISandbox sandbox, CancellationToken cancellationToken)
    {
        var listStep = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ListFiles,
            Path: TrxResultsDir, MaxDepth: 4);
        var listResult = await sandbox.RunStepAsync(listStep, progress: null, cancellationToken);
        if (listResult.ExitCode != 0 || string.IsNullOrEmpty(listResult.OutputContent))
            return TrxSummary.Empty;

        // p0153: ListFiles wire shape is [{path, size_bytes, mtime, is_directory}];
        // legacy shape was string[]. Accept both — extract paths defensively.
        var trxPaths = ExtractPathsEndingWith(listResult.OutputContent, ".trx");
        var summary = TrxSummary.Empty;
        foreach (var trxPath in trxPaths)
            summary = summary.Combine(await ParseSingleAsync(sandbox, trxPath, cancellationToken));
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

    private async Task<TrxSummary> ParseSingleAsync(ISandbox sandbox, string path, CancellationToken cancellationToken)
    {
        var step = new Step(Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.ReadFile, Path: path);
        var result = await sandbox.RunStepAsync(step, progress: null, cancellationToken);
        return result.ExitCode == 0 && result.OutputContent is not null
            ? trxParser.Parse(result.OutputContent)
            : TrxSummary.Empty;
    }

    private static CommandResult BuildResult(TrxSummary summary, int exitCode, string stdout)
    {
        var counters = $"{summary.PassedCount}/{summary.TotalCount} passed, {summary.FailedCount} failed";
        if (summary.FailedCount == 0 && exitCode == 0) return CommandResult.Ok($"Tests passed ({counters})");
        var failureLines = summary.Failures.Select(f => $"  - {f.TestName}: {f.ErrorMessage}");
        var detail = string.Join('\n', failureLines);
        return CommandResult.Fail($"Tests failed ({counters}, exit {exitCode}):\n{detail}\n{stdout}");
    }

    internal sealed record TestCommand(string? Command, IReadOnlyList<string>? Args, bool IsTrxCapable, string? SkipReason)
    {
        public static TestCommand Skip(string reason) => new(null, null, false, reason);
    }
}
