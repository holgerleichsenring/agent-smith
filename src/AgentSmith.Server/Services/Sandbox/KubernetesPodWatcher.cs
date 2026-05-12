using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// Polls Pod status until the toolchain container is Ready, throwing on
/// detected pull / crash / config failures across BOTH the init-container
/// (agent-loader) and the main container (toolchain), or on timeout. On
/// failure or timeout the pod is left in place for `kubectl describe`
/// inspection — the next pipeline-run uses a different unique pod name
/// so it doesn't conflict.
/// </summary>
public sealed class KubernetesPodWatcher(IKubernetes client, ILogger logger)
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private static readonly string[] FatalWaitingReasons =
    [
        "ImagePullBackOff", "ErrImagePull", "InvalidImageName",
        "CrashLoopBackOff", "CreateContainerConfigError",
        "CreateContainerError", "RunContainerError", "ContainerCannotRun"
    ];

    public async Task WaitForReadyAsync(
        string podName, string ns, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        string? lastPhase = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pod = await client.CoreV1.ReadNamespacedPodAsync(podName, ns, cancellationToken: cancellationToken);
            var phase = pod.Status?.Phase;
            if (phase != lastPhase)
            {
                logger.LogInformation("Sandbox pod {Pod} phase: {Phase}", podName, phase);
                lastPhase = phase;
            }
            ThrowIfFailed(podName, pod);
            if (IsToolchainReady(pod)) return;
            await Task.Delay(PollInterval, cancellationToken);
        }

        // Pre-throw diagnostic dump — last-seen pod is fetched once more so the
        // error message includes the actual stuck-container state instead of a
        // generic "did not become ready". Pod is left in place for kubectl describe.
        var finalPod = await TryReadPodAsync(podName, ns, cancellationToken);
        throw new TimeoutException(
            $"Sandbox pod {podName} did not become ready within {timeout.TotalSeconds:F0}s. " +
            $"Pod left in place for inspection: kubectl describe pod {podName} -n {ns}. " +
            $"Last state: {SummarisePodState(finalPod)}");
    }

    private static void ThrowIfFailed(string podName, V1Pod pod)
    {
        if (pod.Status?.Phase == "Failed")
            throw new InvalidOperationException(
                $"Sandbox pod {podName} entered Failed phase: {pod.Status.Reason ?? pod.Status.Message}");

        // Both init and main container statuses can carry the failure reason.
        // The init agent-loader fails here when the carrier image isn't
        // pullable (k8s default to docker.io/library/<no-prefix> when the
        // SandboxSpec.AgentImage doesn't include a registry namespace) — and
        // the previous version of this code only inspected main containers, so
        // the operator only ever saw the 120s timeout instead of the actual
        // ImagePullBackOff signal.
        foreach (var status in pod.Status?.InitContainerStatuses ?? [])
            ThrowIfWaitingFatal(podName, status, "initContainer");
        foreach (var status in pod.Status?.ContainerStatuses ?? [])
            ThrowIfWaitingFatal(podName, status, "container");
    }

    private static void ThrowIfWaitingFatal(string podName, V1ContainerStatus status, string kind)
    {
        var waiting = status.State?.Waiting;
        if (waiting?.Reason is { Length: > 0 } reason && FatalWaitingReasons.Contains(reason))
            throw new InvalidOperationException(
                $"Sandbox pod {podName} {kind} {status.Name} failed with {reason}: " +
                $"{waiting.Message ?? "<no message>"}");
    }

    private static bool IsToolchainReady(V1Pod pod)
    {
        var toolchain = pod.Status?.ContainerStatuses?.FirstOrDefault(s => s.Name == "toolchain");
        return toolchain?.Ready == true;
    }

    private async Task<V1Pod?> TryReadPodAsync(string podName, string ns, CancellationToken ct)
    {
        try { return await client.CoreV1.ReadNamespacedPodAsync(podName, ns, cancellationToken: ct); }
        catch { return null; }
    }

    private static string SummarisePodState(V1Pod? pod)
    {
        if (pod is null) return "<pod-read-failed>";
        var parts = new List<string> { $"phase={pod.Status?.Phase ?? "<unknown>"}" };
        foreach (var s in pod.Status?.InitContainerStatuses ?? [])
            parts.Add(SummariseContainer("initContainer", s));
        foreach (var s in pod.Status?.ContainerStatuses ?? [])
            parts.Add(SummariseContainer("container", s));
        return string.Join("; ", parts);
    }

    private static string SummariseContainer(string kind, V1ContainerStatus s)
    {
        if (s.State?.Waiting is { } waiting)
            return $"{kind} {s.Name}=Waiting({waiting.Reason ?? "?"}: {waiting.Message ?? ""})";
        if (s.State?.Terminated is { } term)
            return $"{kind} {s.Name}=Terminated(exit={term.ExitCode}, {term.Reason ?? "?"})";
        if (s.State?.Running is not null)
            return $"{kind} {s.Name}=Running(ready={s.Ready})";
        return $"{kind} {s.Name}=<unknown>";
    }
}
