using AgentSmith.Contracts.Sandbox;
using Docker.DotNet;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0201/p0237: one docker inspect of a sandbox's toolchain container, read as a verdict. Lifted out of
/// <see cref="SandboxLivenessWatcher"/> by 2026-10-13-9a59i so the release reads the same evidence the
/// watcher acts on: Running, an exit with facts (exit code, OOMKilled), or Gone/Unknown/ProbeError with none.
/// </summary>
public sealed class DockerContainerProbe(IDockerClient docker)
{
    public async Task<DockerContainerVerdict> ProbeAsync(string containerId, CancellationToken ct)
    {
        try
        {
            var info = await docker.Containers.InspectContainerAsync(containerId, ct);
            var state = info.State;
            if (state?.Running == true) return new DockerContainerVerdict(true, "Running");
            if (state is null) return new DockerContainerVerdict(false, "Unknown");
            // p0237: capture the EXIT EVIDENCE (exit code + OOMKilled + error) so the vanish says WHY
            // the container died — OOM (137 + OOMKilled), a crash (139 = SIGSEGV), or a clean exit (0).
            var label = state.Dead ? "Dead"
                : !string.IsNullOrEmpty(state.Status) ? state.Status
                : $"Exited({state.ExitCode})";
            return new DockerContainerVerdict(
                false, label, state.ExitCode, state.OOMKilled, state.Error, HasExitFacts: true);
        }
        catch (DockerContainerNotFoundException)
        {
            return new DockerContainerVerdict(false, "Gone");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DockerContainerVerdict(false, "ProbeError", Error: ex.Message);
        }
    }
}

/// <summary>
/// HasExitFacts: true only when docker inspect actually delivered the exit state — Gone/Unknown/ProbeError
/// verdicts carry defaults, not evidence.
/// </summary>
public readonly record struct DockerContainerVerdict(
    bool IsRunning, string State, long ExitCode = 0, bool OomKilled = false, string? Error = null,
    bool HasExitFacts = false)
{
    /// <summary>2026-10-13-9a59i: the verdict as the release records it.</summary>
    public SandboxExitRead ToExitRead() =>
        IsRunning ? SandboxExitRead.Running
        : HasExitFacts ? SandboxExitRead.Exited((int)ExitCode, OomKilled)
        : SandboxExitRead.Gone;
}
