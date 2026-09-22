using Docker.DotNet.Models;

namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: reads a Docker sandbox container's labels into the facts
/// <see cref="SandboxReapJudge"/> decides on. The label spelling is the backend's
/// knowledge and stops here; the judge never sees a Docker type.
/// </summary>
public static class SandboxContainerCandidates
{
    public static IReadOnlyList<SandboxReapCandidate> From(
        IEnumerable<ContainerListResponse> containers, DateTimeOffset now) =>
        [.. containers.Select(container => new SandboxReapCandidate(
            container.ID,
            LabelOrEmpty(container.Labels, DockerContainerSpecBuilder.JobIdLabel),
            LabelOrEmpty(container.Labels, DockerContainerSpecBuilder.RunIdLabel),
            LabelOrEmpty(container.Labels, DockerContainerSpecBuilder.ConversationIdLabel),
            now - new DateTimeOffset(container.Created, TimeSpan.Zero)))];

    private static string LabelOrEmpty(IDictionary<string, string>? labels, string key) =>
        labels is not null && labels.TryGetValue(key, out var value) ? value : string.Empty;
}
