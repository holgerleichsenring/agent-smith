using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// Regression: k8s namespaces with a ResourceQuota that mandates limits.cpu
/// and limits.memory on every container (init + main) reject the pod when
/// any container is missing limits. The agent-loader initContainer was
/// missing limits — pod create fails with "must specify limits.cpu for:
/// agent-loader" before the workload starts.
/// </summary>
public sealed class PodSpecBuilderResourcesTests
{
    private static readonly PodSpecBuilder Builder = new();

    private static readonly SandboxSpec MinimalSpec = new(
        ToolchainImage: "debian:bookworm",
        Resources: ResourceLimits.Default,
        AgentImage: "agent-smith-sandbox-agent:latest");

    [Fact]
    public void Build_LoaderInitContainer_HasCpuAndMemoryLimits()
    {
        var pod = Builder.Build(
            podName: "agentsmith-sandbox-abc",
            jobId: "abc",
            redisUrl: "redis:6379",
            spec: MinimalSpec,
            owner: null);

        var loader = pod.Spec.InitContainers.Single(c => c.Name == "agent-loader");
        loader.Resources.Should().NotBeNull();
        loader.Resources.Limits.Should().ContainKey("cpu");
        loader.Resources.Limits.Should().ContainKey("memory");
    }

    [Fact]
    public void Build_LoaderInitContainer_HasCpuAndMemoryRequests()
    {
        var pod = Builder.Build("p", "j", "redis:6379", MinimalSpec, owner: null);

        var loader = pod.Spec.InitContainers.Single(c => c.Name == "agent-loader");
        loader.Resources.Requests.Should().ContainKey("cpu");
        loader.Resources.Requests.Should().ContainKey("memory");
    }

    [Fact]
    public void Build_ToolchainContainer_PopulatesBothRequestsAndLimits()
    {
        // CPU values stay sub-core so K8s' ResourceQuantity round-trip
        // does not normalise them (e.g. "2000m" canonicalises to "2").
        var spec = new SandboxSpec(
            ToolchainImage: "node:20",
            Resources: new ResourceLimits("250m", "750m", "768Mi", "3Gi"),
            AgentImage: "agent:1");
        var pod = Builder.Build("p", "j", "redis:6379", spec, owner: null);

        var toolchain = pod.Spec.Containers.Single(c => c.Name == "toolchain");
        toolchain.Resources.Requests["cpu"].ToString().Should().Be("250m");
        toolchain.Resources.Requests["memory"].ToString().Should().Be("768Mi");
        toolchain.Resources.Limits["cpu"].ToString().Should().Be("750m");
        toolchain.Resources.Limits["memory"].ToString().Should().Be("3Gi");
    }
}
