using System.Diagnostics;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Tools;

/// <summary>
/// Runs tools as local processes. No container runtime needed.
/// {work} in arguments is resolved to a temp directory.
/// Tools must be installed and available in PATH.
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
        var workDir = Path.Combine(Path.GetTempPath(), $"{request.Tool}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            // Write input files to working directory
            if (request.InputFiles is not null)
                foreach (var (name, content) in request.InputFiles)
                    await File.WriteAllTextAsync(Path.Combine(workDir, name), content, cancellationToken);

            var resolvedArgs = DockerToolRunner.ResolveArguments(request.Arguments, workDir);

            logger.LogInformation("Running {Tool}: {Binary} {Args}",
                request.Tool, binary, string.Join(" ", resolvedArgs));

            var sw = Stopwatch.StartNew();

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = binary,
                    Arguments = string.Join(" ", resolvedArgs),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = workDir,
                }
            };

            var stdout = new System.Text.StringBuilder();
            var stderr = new System.Text.StringBuilder();

            process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                stderr.AppendLine(e.Data);
                logger.LogInformation("[{Tool}] {Line}", request.Tool, e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));

            try { await process.WaitForExitAsync(timeoutCts.Token); }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Process {Tool} timed out after {Timeout}s",
                    request.Tool, request.TimeoutSeconds);
                try { process.Kill(entireProcessTree: true); } catch { }
            }

            sw.Stop();

            string? outputContent = null;
            if (request.OutputFileName is not null)
            {
                var outputPath = Path.Combine(workDir, request.OutputFileName);
                if (File.Exists(outputPath))
                    outputContent = await File.ReadAllTextAsync(outputPath, CancellationToken.None);
            }

            logger.LogInformation("{Tool} exited with code {Code} in {Duration}s",
                request.Tool, process.ExitCode, (int)sw.Elapsed.TotalSeconds);

            return new ToolResult(stdout.ToString(), stderr.ToString(), outputContent,
                process.ExitCode, (int)sw.Elapsed.TotalSeconds);
        }
        finally
        {
            if (Directory.Exists(workDir))
                Directory.Delete(workDir, recursive: true);
        }
    }
}
