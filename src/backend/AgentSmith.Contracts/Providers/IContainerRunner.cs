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

/// <summary>
/// What a tool container left behind. <see cref="CutOff"/> is set when the runner
/// stopped the container at <see cref="ContainerRunRequest.TimeoutSeconds"/> rather
/// than the tool deciding it was finished: only the runner knows this, and without it
/// a killed container is indistinguishable from one that exited non-zero on its own.
/// Its output is then whatever the tool had produced by that moment, not a result.
/// </summary>
public sealed record ContainerResult(
    string Stdout,
    string Stderr,
    int ExitCode,
    int DurationSeconds,
    bool CutOff = false);
