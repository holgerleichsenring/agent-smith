using AgentSmith.Contracts.Sandbox;
using k8s;
using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-13-9a59i: reads how a sandbox pod's toolchain container stands before the release deletes
/// the pod. RestartPolicy is Never, so a container that died mid-run is still Terminated here with its
/// exit code and reason. A pod that is gone, or a read that fails, is Gone — never folded into Running.
/// </summary>
public sealed class KubernetesPodExitReader(IKubernetes client)
{
    private const string ToolchainContainer = "toolchain";
    private const string OomKilledReason = "OOMKilled";

    public async Task<SandboxExitRead> ReadAsync(string podName, string @namespace, CancellationToken ct)
    {
        try
        {
            var pod = await client.CoreV1.ReadNamespacedPodAsync(podName, @namespace, cancellationToken: ct);
            return Of(pod);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // HttpOperationException NotFound when the pod was deleted under the run; any other
            // failure leaves the state unreadable — both are Gone.
            return SandboxExitRead.Gone;
        }
    }

    internal static SandboxExitRead Of(V1Pod? pod)
    {
        var toolchain = pod?.Status?.ContainerStatuses?.FirstOrDefault(s => s.Name == ToolchainContainer);
        if (toolchain?.State is not { } state) return SandboxExitRead.Gone;
        if (state.Terminated is { } terminated)
            return SandboxExitRead.Exited(terminated.ExitCode, terminated.Reason == OomKilledReason);
        return SandboxExitRead.Running;
    }
}
