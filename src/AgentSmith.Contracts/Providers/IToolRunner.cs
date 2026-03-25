namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Runs external security tools (Nuclei, Spectral, etc.) and returns their output.
/// Implementations handle the execution environment: Docker containers, K8s Jobs,
/// Podman, or local processes.
///
/// Arguments may contain the placeholder {work} which each runner resolves
/// to its working directory (e.g. /work in Docker, a temp dir for processes).
/// Input files are placed in {work}, output files are read from {work}.
/// Spawners never reference container-internal paths directly.
/// </summary>
public interface IToolRunner
{
    Task<ToolResult> RunAsync(ToolRunRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Request to run an external tool.
/// Arguments use {work} as placeholder for the working directory.
/// The runner resolves {work} to its execution environment.
/// InputFiles are written to {work}/ before the tool starts.
/// OutputFileName is read from {work}/ after the tool finishes.
/// </summary>
public sealed record ToolRunRequest(
    string Tool,
    IReadOnlyList<string> Arguments,
    Dictionary<string, string>? InputFiles = null,
    string? OutputFileName = null,
    Dictionary<string, string>? ExtraHosts = null,
    int TimeoutSeconds = 300);

public sealed record ToolResult(
    string Stdout,
    string Stderr,
    string? OutputFileContent,
    int ExitCode,
    int DurationSeconds);
