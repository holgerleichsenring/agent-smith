using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Tools;

/// <summary>
/// Runs tools as local processes. No container runtime needed.
/// Tools must be installed and available in PATH or configured with explicit paths.
/// </summary>
public sealed class ProcessToolRunner(
    ILogger<ProcessToolRunner> logger) : IToolRunner
{
    private static readonly Dictionary<string, string> DefaultBinaries = new()
    {
        ["nuclei"] = "nuclei",
        ["spectral"] = "spectral",
    };

    public async Task<ToolResult> RunAsync(
        ToolRunRequest request, CancellationToken cancellationToken)
    {
        var binary = DefaultBinaries.GetValueOrDefault(request.Tool, request.Tool);
        var tempDir = Path.Combine(Path.GetTempPath(), $"{request.Tool}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Write input files to temp directory
            if (request.InputFiles is not null)
            {
                foreach (var (fileName, content) in request.InputFiles)
                    await File.WriteAllTextAsync(
                        Path.Combine(tempDir, fileName), content, cancellationToken);
            }

            // Rewrite /input/ references to temp directory in arguments
            var args = request.Arguments
                .Select(a => a.Replace("/input/", $"{tempDir}/"))
                .ToList();

            logger.LogInformation("Running {Tool}: {Binary} {Args}",
                request.Tool, binary, string.Join(" ", args));

            var sw = Stopwatch.StartNew();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = binary,
                    Arguments = string.Join(" ", args),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = tempDir,
                }
            };

            var stdoutBuilder = new System.Text.StringBuilder();
            var stderrBuilder = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    stdoutBuilder.AppendLine(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    stderrBuilder.AppendLine(e.Data);
                    logger.LogInformation("[{Tool}] {Line}", request.Tool, e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Process {Tool} timed out after {Timeout}s",
                    request.Tool, request.TimeoutSeconds);

                try { process.Kill(entireProcessTree: true); }
                catch { /* best effort */ }
            }

            sw.Stop();

            // Read output file if requested
            string? outputContent = null;
            if (request.OutputFileName is not null)
            {
                var outputPath = Path.Combine(tempDir, request.OutputFileName);
                if (File.Exists(outputPath))
                    outputContent = await File.ReadAllTextAsync(outputPath, CancellationToken.None);
            }

            logger.LogInformation("{Tool} exited with code {Code} in {Duration}s",
                request.Tool, process.ExitCode, (int)sw.Elapsed.TotalSeconds);

            return new ToolResult(
                stdoutBuilder.ToString(),
                stderrBuilder.ToString(),
                outputContent,
                process.ExitCode,
                (int)sw.Elapsed.TotalSeconds);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
