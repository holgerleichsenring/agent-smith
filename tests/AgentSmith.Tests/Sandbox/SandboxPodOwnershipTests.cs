using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0465: the Kubernetes half. A namespace is shared, and the corpse reaper used to
/// select every <c>app=agentsmith-sandbox</c> pod in it. The stamp and the selector
/// are one type so they cannot drift apart.
/// </summary>
public sealed class SandboxPodOwnershipTests
{
    private static readonly SandboxOwnerIdentity Owner = new("store-0123456789abcdef");
    private readonly SandboxPodLabels _labels = new(Owner);

    [Fact]
    public void OwnedSelector_ScopesTheSweepToThisLivenessStore()
    {
        _labels.OwnedSelector.Should().Be($"app={SandboxPodLabels.AppLabel},owner={Owner.Value}");
    }

    [Fact]
    public void UnownedSelector_AsksForPodsFromABinaryBeforeThisPhase()
    {
        SandboxPodLabels.UnownedSelector.Should().Be($"app={SandboxPodLabels.AppLabel},!owner");
    }

    [Fact]
    public void Build_StampsTheOwner_SoTheOwnedSelectorCanFindThePodBack()
    {
        var labels = _labels.Build("job-1", "run-1");

        labels[SandboxPodLabels.OwnerLabel].Should().Be(Owner.Value);
        labels["app"].Should().Be(SandboxPodLabels.AppLabel);
        labels["pipeline-id"].Should().Be("job-1");
        labels[SandboxPodLabels.RunIdLabel].Should().Be("run-1");
    }

    [Fact]
    public void Build_RunlessSandbox_StillCarriesTheOwner()
    {
        var labels = _labels.Build("job-1", runId: null);

        labels.Should().ContainKey(SandboxPodLabels.OwnerLabel);
        labels.Should().NotContainKey(SandboxPodLabels.RunIdLabel);
    }

    [Fact]
    public void PodSpecBuilder_StampsTheOwnerOnEveryPodItBuilds()
    {
        var pod = new PodSpecBuilder(_labels).Build(
            "agentsmith-sandbox-abc", "job-1", "redis:6379",
            new SandboxSpec("node:20", ResourceLimits.Default, "agent:1", RunId: "run-1"), owner: null);

        pod.Metadata.Labels[SandboxPodLabels.OwnerLabel].Should().Be(Owner.Value);
    }
}
