using CommandLineStringSplitter = System.CommandLine.Parsing.CommandLineStringSplitter;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0202e: runs one sandbox key's context.yaml <c>prerequisites</c> command at its repository root — a command needing
/// another directory carries its own cd, because meta.workdir says where source lives, not the manifest.
/// 2026-11-25-9992b: shared by EnsurePrerequisites and the workspace restart that restores a repository.
/// </summary>
public sealed class PrerequisiteInstall(ILogger<PrerequisiteInstall> logger)
{
    // The backend applies sandbox.step_timeout_seconds (p0200), the single knob that changes this.
    private const int InstallTimeoutSeconds = 900;

    public async Task<PrerequisiteOutcome> RunAsync(
        string key, ISandbox sandbox, string? command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            logger.LogInformation("{Key}: no context.yaml prerequisites — the coding agent installs deps as needed; skipping", key);
            return new PrerequisiteOutcome(key, ExitCode: 0, Skipped: true);
        }
        var tokens = CommandLineStringSplitter.Instance.Split(command).ToList();
        if (tokens.Count == 0)
        {
            logger.LogWarning("{Key}: install command '{Raw}' tokenized to zero arguments; skipping", key, command);
            return new PrerequisiteOutcome(key, ExitCode: 0, Skipped: true);
        }
        var root = sandbox.WorkDirectory();
        logger.LogInformation("{Key}: installing dependencies via {Command} at {Cwd}", key, command, root);
        var result = await sandbox.RunStepAsync(new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: tokens[0], Args: tokens.Skip(1).ToArray(),
            WorkingDirectory: root, TimeoutSeconds: InstallTimeoutSeconds), progress: null, cancellationToken);
        var output = string.Join("\n", new[] { result.OutputContent, result.ErrorMessage }
            .Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        // Surface WHY: without this the operator only sees "failed in repo X" — undiagnosable.
        if (result.ExitCode != 0)
            logger.LogError("{Key}: '{Command}' failed (exit {Exit}) at {Cwd}:\n{Output}",
                key, command, result.ExitCode, root, output.Length <= 4000 ? output : "…" + output[^4000..]);
        return new PrerequisiteOutcome(key, result.ExitCode, Skipped: false, Output: output);
    }
}
