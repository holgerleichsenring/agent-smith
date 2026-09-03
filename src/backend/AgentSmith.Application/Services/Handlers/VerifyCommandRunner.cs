using System.Text;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// p0419: runs ONE declared verification command in a sandbox and reports what it said.
/// <para>
/// Split out of VerifyPhaseHandler, which decides WHICH commands to run and what their
/// outcomes mean. Executing one is a different job, and it is the job that failed
/// silently: run 354b reported two red builds whose reason was blank everywhere.
/// </para>
/// </summary>
public sealed class VerifyCommandRunner(ILogger<VerifyCommandRunner> logger)
{
    // Build and test on a cold sandbox are the slowest deterministic steps in the run.
    // The sandbox backend still applies the operator's sandbox.step_timeout_seconds cap,
    // so this is a ceiling rather than a second knob.
    private const int VerifyTimeoutSeconds = 1800;
    private const int OutputTailChars = 4000;

    public async Task<VerifyOutcome> RunAsync(
        string key, string stage, ISandbox sandbox, string workingDirectory,
        string rawCommand, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sandbox);
        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            logger.LogWarning(
                "{Key}: {Stage} command is blank; treating as absent", key, stage);
            return new VerifyOutcome(
                key, stage, rawCommand, ExitCode: 0, Skipped: true, Cwd: workingDirectory);
        }

        logger.LogInformation("{Key}: verifying via {Stage} command '{Command}' at {Cwd}",
            key, stage, rawCommand, workingDirectory);
        // p0425: a declared command is a command LINE, run through the same shell as the
        // agent's own run_command tool. Tokenising it into argv handed `&&` to MSBuild —
        // ticket 19192's verification declared two test projects and was failed by its own
        // separator, after both phases of real work had succeeded. The verifier decides
        // whether work is delivered; it must not be the weaker executor.
        var step = new Step(
            Step.CurrentSchemaVersion, Guid.NewGuid(), StepKind.Run,
            Command: "/bin/sh", Args: ["-c", rawCommand],
            WorkingDirectory: workingDirectory, TimeoutSeconds: VerifyTimeoutSeconds);

        // The gate build used to run with progress: null. The one command whose outcome
        // decides the run streamed to nobody — not the dashboard, not the log — and when
        // the sandbox dropped its stdout too, a red build had no reason left to give.
        // What passes through here is kept, whatever the sandbox reports afterwards.
        var streamed = new StringBuilder();
        var progress = new SyncProgress<StepEvent>(ev =>
        {
            if (ev.Kind is StepEventKind.Stdout or StepEventKind.Stderr)
                streamed.AppendLine(ev.Line);
        });

        var result = await sandbox.RunStepAsync(step, progress, ct);
        var output = Combine(result.OutputContent, result.ErrorMessage);
        if (string.IsNullOrWhiteSpace(output)) output = streamed.ToString().TrimEnd();

        if (result.ExitCode != 0)
            // Surface WHY on the spot: the operator sees a red run, and without the tail
            // the only way to learn what broke is to reproduce the whole run.
            logger.LogError("{Key}: {Stage} '{Command}' failed (exit {Exit}) at {Cwd}:\n{Output}",
                key, stage, rawCommand, result.ExitCode, workingDirectory,
                Tail(output, OutputTailChars));

        return new VerifyOutcome(
            key, stage, rawCommand, result.ExitCode, Skipped: false, Output: output,
            Cwd: workingDirectory);
    }

    private static string Combine(string? outputContent, string? errorMessage) =>
        string.Join("\n", new[] { outputContent, errorMessage }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string Tail(string text, int maxChars) =>
        text.Length <= maxChars ? text : text[^maxChars..];
}
