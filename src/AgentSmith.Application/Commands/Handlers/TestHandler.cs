using System.Diagnostics;
using AgentSmith.Application.Commands.Contexts;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Commands.Handlers;

/// <summary>
/// Runs project-specific tests against the code changes.
/// </summary>
public sealed class TestHandler(
    ILogger<TestHandler> logger)
    : ICommandHandler<TestContext>
{
    private const int TestTimeoutSeconds = 300;

    public async Task<CommandResult> ExecuteAsync(
        TestContext context, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Running tests for {Changes} changes...", context.Changes.Count);

        var repoPath = context.Repository.LocalPath;
        var testCommand = DetectTestCommand(repoPath);

        if (testCommand is null)
        {
            logger.LogWarning("No test framework detected, skipping tests");
            context.Pipeline.Set(ContextKeys.TestResults, "No test framework detected");
            return CommandResult.Ok("No test framework detected, skipping tests");
        }

        var (exitCode, result) = await RunTestsAsync(repoPath, testCommand, cancellationToken);
        context.Pipeline.Set(ContextKeys.TestResults, result);

        if (exitCode != 0)
        {
            return CommandResult.Fail($"Tests failed (exit code {exitCode}):\n{result}");
        }

        return CommandResult.Ok("Tests passed");
    }

    private static string? DetectTestCommand(string repoPath)
    {
        if (Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories).Length > 0)
            return "dotnet test --no-restore --verbosity minimal";

        if (File.Exists(Path.Combine(repoPath, "package.json")))
            return "npm test";

        if (File.Exists(Path.Combine(repoPath, "pytest.ini"))
            || File.Exists(Path.Combine(repoPath, "pyproject.toml")))
            return "pytest";

        return null;
    }

    private async Task<(int ExitCode, string Output)> RunTestsAsync(
        string repoPath, string command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Running: {Command}", command);

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-c \"{command}\"",
            WorkingDirectory = repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(TestTimeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            return (-1, "Test execution timed out.");
        }

        var stdout = await outputTask;
        var stderr = await errorTask;

        var output = string.IsNullOrEmpty(stderr)
            ? stdout
            : $"{stdout}\n[stderr]\n{stderr}";

        return (process.ExitCode, output);
    }
}
