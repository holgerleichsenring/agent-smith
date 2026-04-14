using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Security;

/// <summary>
/// Runs external CLI processes (npm, pip-audit, dotnet) with timeout and captures output.
/// </summary>
internal sealed class AuditProcessRunner(ILogger logger)
{
    private const int ProcessTimeoutSeconds = 60;

    internal async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(ProcessTimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                logger.LogWarning("{FileName} timed out after {Timeout}s", fileName, ProcessTimeoutSeconds);
                return (-1, string.Empty, "Process timed out");
            }

            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            return (process.ExitCode, stdout, stderr);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            logger.LogDebug(ex, "{FileName} not found on PATH", fileName);
            return (-1, string.Empty, $"{fileName} not found: {ex.Message}");
        }
    }
}
