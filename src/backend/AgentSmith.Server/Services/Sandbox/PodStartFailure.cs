using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-10-16-8648: what a failed sandbox pod is reported as. A pod that fails at start rarely
/// carries a pod-level reason — a live run said "Failed" and nothing else when the toolchain
/// could not exec the agent. The container that exited carries the code and the reason.
/// </summary>
internal static class PodStartFailure
{
    public static string Describe(string podName, V1Pod pod)
    {
        var podReason = pod.Status?.Reason ?? pod.Status?.Message;
        var exited = (pod.Status?.InitContainerStatuses ?? [])
            .Select(s => (Kind: "initContainer", Status: s))
            .Concat((pod.Status?.ContainerStatuses ?? []).Select(s => (Kind: "container", Status: s)))
            .Where(c => c.Status.State?.Terminated is { ExitCode: not 0 })
            .Select(c => DescribeTermination(c.Kind, c.Status));
        var detail = string.Join("; ", new[] { podReason }.Concat(exited).Where(p => !string.IsNullOrEmpty(p)));
        return $"Sandbox pod {podName} failed: {(detail.Length > 0 ? detail : "no reason reported")}";
    }

    private static string DescribeTermination(string kind, V1ContainerStatus status)
    {
        var term = status.State!.Terminated!;
        var message = string.IsNullOrWhiteSpace(term.Message) ? "" : $" — {term.Message.Trim()}";
        return $"{kind} {status.Name} exited {term.ExitCode} ({term.Reason ?? "no reason"}){message}";
    }
}
