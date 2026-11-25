using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using k8s.Models;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-11-25-9992b: a run's workspace pod carries a toolchain container per image — each its own agent, job id, bus
/// URL and (hardened) home — over one /work and one agent loader, and names every job id for the reapers.
/// </summary>
public sealed class PodSpecBuilderWorkspaceTests
{
    private static readonly PodSpecBuilder Builder =
        new(new SandboxPodLabels(new SandboxOwnerIdentity("store-test")), new PodSecurity(new KubernetesSandboxOptions()));

    private static V1Pod Build() => Builder.Build("ws",
        [
            new PodToolchain("dotnet:9", ResourceLimits.Default, "job-a", "redis://a"),
            new PodToolchain("node:20", new ResourceLimits { MemoryLimit = "1Gi" }, "job-b", "redis://b"),
        ],
        new SandboxSpec("ignored", ResourceLimits.Default, "agent:1", SecurityContext: SandboxSecurityContext.Hardened,
            RunId: "run-1"),
        owner: null);

    [Fact]
    public void Build_TwoToolchains_TwoContainersEachWithItsOwnAgent()
    {
        var pod = Build();

        pod.Spec.Containers.Select(c => c.Name).Should().Equal("toolchain-0", "toolchain-1");
        pod.Spec.Containers.Select(c => c.Image).Should().Equal("dotnet:9", "node:20");
        Env(pod, 0)["JOB_ID"].Should().Be("job-a");
        Env(pod, 1)["JOB_ID"].Should().Be("job-b");
        Env(pod, 0)["REDIS_URL"].Should().Be("redis://a");
        Env(pod, 1)["REDIS_URL"].Should().Be("redis://b");
        pod.Spec.Containers[1].Args.Should().Equal("--job-id", "job-b", "--run-id", "run-1");
        pod.Spec.Containers[1].Resources.Limits["memory"].ToString().Should().Be("1Gi");
    }

    [Fact]
    public void Build_TwoToolchains_OneLoaderOneWorkAndAHomePerContainer()
    {
        var pod = Build();

        pod.Spec.InitContainers.Should().ContainSingle().Which.Name.Should().Be("agent-loader");
        pod.Spec.Volumes.Should().ContainSingle(v => v.Name == PodToolchainContainer.WorkVolume);
        pod.Spec.Containers.Should().OnlyContain(c =>
            c.VolumeMounts.Any(m => m.Name == PodToolchainContainer.WorkVolume && m.MountPath == "/work"));
        var homes = pod.Spec.Containers.Select(c => c.VolumeMounts.Single(m => m.MountPath == "/root").Name).ToList();
        homes.Should().Equal(PodSecurity.HomeVolumeFor(0), PodSecurity.HomeVolumeFor(1));
        pod.Spec.Volumes.Select(v => v.Name).Should().Contain(homes);
        pod.Spec.Containers.Should().OnlyContain(c => c.VolumeMounts.Any(m => m.MountPath == "/shared" && m.ReadOnlyProperty == true));
    }

    [Fact]
    public void Build_TwoToolchains_ShareNoProcessNamespaceAndNameEveryJob()
    {
        var pod = Build();

        pod.Spec.ShareProcessNamespace.Should().BeNull("no toolchain may see another's processes");
        pod.Metadata.Labels[SandboxPodLabels.PipelineIdLabel].Should().Be("job-a");
        SandboxPodLabels.SplitJobIds(pod.Metadata.Annotations[SandboxPodLabels.JobIdsAnnotation])
            .Should().Equal("job-a", "job-b");
        SandboxPodCandidates.JobIds(pod).Should().Equal("job-a", "job-b");
    }

    private static Dictionary<string, string> Env(V1Pod pod, int index) =>
        pod.Spec.Containers[index].Env.Where(e => e.Value is not null).ToDictionary(e => e.Name, e => e.Value);
}
