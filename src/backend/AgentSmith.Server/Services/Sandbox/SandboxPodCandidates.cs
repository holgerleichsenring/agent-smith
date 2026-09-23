using k8s.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: reads a sandbox pod's labels into the facts
/// <see cref="SandboxReapJudge"/> decides on. Before this the corpse selection
/// returned a pod name and a run id and read no other label, so a conversation stamp
/// could not have reached the decision at all. A pod with no name is dropped: nothing
/// can be deleted by a name that is not there.
/// </summary>
public static class SandboxPodCandidates
{
    public static IReadOnlyList<SandboxReapCandidate> From(IEnumerable<V1Pod> pods, DateTimeOffset now) =>
        [.. pods.Where(pod => !string.IsNullOrEmpty(pod.Metadata?.Name))
            .Select(pod => new SandboxReapCandidate(
                pod.Metadata!.Name,
                LabelOrEmpty(pod, SandboxPodLabels.PipelineIdLabel),
                LabelOrEmpty(pod, SandboxPodLabels.RunIdLabel),
                LabelOrEmpty(pod, SandboxPodLabels.ConversationIdLabel),
                Age(pod, now)))];

    // No creation timestamp at all is treated as old, which is what the corpse sweep
    // has always done: an undatable pod cannot be saved by the spawn-window rail.
    private static TimeSpan Age(V1Pod pod, DateTimeOffset now)
    {
        var created = pod.Metadata?.CreationTimestamp;
        return created is null
            ? TimeSpan.MaxValue
            : now - new DateTimeOffset(DateTime.SpecifyKind(created.Value, DateTimeKind.Utc));
    }

    private static string LabelOrEmpty(V1Pod pod, string key) =>
        pod.Metadata?.Labels is { } labels && labels.TryGetValue(key, out var value) ? value : string.Empty;
}
