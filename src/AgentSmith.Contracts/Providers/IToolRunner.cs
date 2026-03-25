namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Runs external security tools (Nuclei, Spectral, etc.) and returns their output.
/// Implementations handle the execution environment: Docker containers, K8s Jobs,
/// Podman, or local processes.
/// </summary>
public interface IToolRunner
{
    Task<ToolResult> RunAsync(ToolRunRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Request to run an external tool.
/// The runner resolves the tool name to a container image or local binary.
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
