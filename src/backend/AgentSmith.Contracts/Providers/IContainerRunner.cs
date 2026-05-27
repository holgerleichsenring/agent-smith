namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Runs a short-lived container and returns its stdout output.
/// Used for tool containers (Nuclei, etc.) that produce results on stdout.
/// Implementations: DockerContainerRunner (local/Docker), K8sContainerRunner (K8s).
/// </summary>
public interface IContainerRunner
{
    Task<ContainerResult> RunAsync(ContainerRunRequest request, CancellationToken cancellationToken);
}

public sealed record ContainerRunRequest(
    string Image,
    IReadOnlyList<string> Command,
    Dictionary<string, string>? VolumeMounts = null,
    Dictionary<string, string>? ExtraHosts = null,
    int TimeoutSeconds = 300);

public sealed record ContainerResult(
    string Stdout,
    string Stderr,
    int ExitCode,
    int DurationSeconds);
