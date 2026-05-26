using System.Text;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Sandbox.Wire;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Runs external CLI processes (npm, pip-audit, dotnet) inside the sandbox and
/// captures stdout/stderr. Routes through Step{Kind=Run} so the audit hits the
/// same toolchain image the agent uses.
/// </summary>
internal sealed class AuditProcessRunner(ILogger logger)
{
    private const int ProcessTimeoutSeconds = 60;

    internal async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        ISandbox sandbox, string command, CancellationToken cancellationToken)
    {
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var step = new Step(
            SchemaVersion: Step.CurrentSchemaVersion,
            StepId: Guid.NewGuid(),
            Kind: StepKind.Run,
            Command: "/bin/sh",
            Args: ["-c", command],
            WorkingDirectory: Repository.SandboxWorkPath,
            TimeoutSeconds: ProcessTimeoutSeconds);

        var progress = new Progress<StepEvent>(ev =>
        {
            if (ev.Kind == StepEventKind.Stdout) stdout.AppendLine(ev.Line);
            else if (ev.Kind == StepEventKind.Stderr) stderr.AppendLine(ev.Line);
        });

        var result = await sandbox.RunStepAsync(step, progress, cancellationToken);
        if (result.TimedOut)
        {
            logger.LogWarning("Sandbox command timed out after {Timeout}s: {Command}",
                ProcessTimeoutSeconds, command);
            return (-1, stdout.ToString(), "Process timed out");
        }

        return (result.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
