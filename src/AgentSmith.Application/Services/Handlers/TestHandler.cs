using AgentSmith.Application.Models;
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
/// Test-command resolution is ProjectMap-driven (no Directory.GetFiles scans).
/// </summary>
public sealed class TestHandler(
    ILogger<TestHandler> logger)
    : ICommandHandler<TestContext>
{
    private const int TestTimeoutSeconds = 300;

    public async Task<CommandResult> ExecuteAsync(
        TestContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running tests for {Changes} changes...", context.Changes.Count);

        var (command, args) = ResolveTestCommand(context.Pipeline);
        if (command is null)
        {
            logger.LogWarning("No test framework detected in ProjectMap, skipping tests");
            context.Pipeline.Set(ContextKeys.TestResults, "No test framework detected");
            return CommandResult.Ok("No test framework detected, skipping tests");
        }

        if (!context.Pipeline.TryGet<ISandbox>(ContextKeys.Sandbox, out var sandbox) || sandbox is null)
            return SkipWithoutSandbox(context);

        return await RunInSandboxAsync(context, sandbox, command, args, cancellationToken);
    }

    private (string? command, IReadOnlyList<string>? args) ResolveTestCommand(PipelineContext pipeline)
    {
        if (pipeline.TryGet<DetectedProject>(ContextKeys.DetectedProject, out var detected)
            && detected?.TestCommand is not null)
        {
            logger.LogInformation("Using detected test command: {Command}", detected.TestCommand);
            return ("/bin/sh", new[] { "-c", detected.TestCommand });
        }

        if (!pipeline.TryGet<ProjectMap>(ContextKeys.ProjectMap, out var projectMap) || projectMap is null)
            return (null, null);

        return projectMap.PrimaryLanguage.ToLowerInvariant() switch
        {
            "dotnet" or "dotnet8" or "dotnet9" or "csharp"
                => ("dotnet", new[] { "test", "--verbosity", "minimal" }),
            "node" or "node20" or "javascript" or "typescript"
                => ("npm", new[] { "test" }),
            "python" or "python3"
                => ("pytest", Array.Empty<string>()),
            _ => (null, null)
        };
    }

    private CommandResult SkipWithoutSandbox(TestContext context)
    {
        // A missing sandbox means we cannot exercise the project's tests at all.
        // Returning Ok would falsely advance the pipeline (commit + PR) without test
        // validation. Surface as failure so the operator sees the broken sandbox path.
        logger.LogError("No sandbox in pipeline context — failing test step");
        context.Pipeline.Set(ContextKeys.TestResults, "Failed (no sandbox)");
        return CommandResult.Fail(
            "Test execution requires an active sandbox; none is present in the pipeline context. " +
            "This usually means the sandbox factory failed to create a runtime (RBAC, image pull, " +
            "or local Docker socket). Check the Server-Pod's startup logs for sandbox-creation errors.");
    }

    private async Task<CommandResult> RunInSandboxAsync(
        TestContext context, ISandbox sandbox,
        string command, IReadOnlyList<string>? args, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running in sandbox: {Command} {Args}", command, string.Join(' ', args ?? []));
        var stdout = new System.Text.StringBuilder();
        var progress = new Progress<StepEvent>(ev =>
        {
            if (ev.Kind is StepEventKind.Stdout or StepEventKind.Stderr)
                stdout.AppendLine(ev.Line);
        });

        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: command, Args: args, TimeoutSeconds: TestTimeoutSeconds);
        var result = await sandbox.RunStepAsync(step, progress, cancellationToken);

        var output = stdout.ToString();
        context.Pipeline.Set(ContextKeys.TestResults, output);

        if (result.ExitCode != 0)
            return CommandResult.Fail($"Tests failed (exit code {result.ExitCode}):\n{output}");
        return CommandResult.Ok("Tests passed");
    }
}
