using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using k8s.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0269a: the capacity probes decide whether a run's sandbox footprint fits before
/// it is claimed. k8s reads the namespace ResourceQuota (pure hard-vs-used math,
/// tested directly); Docker counts labelled containers against a configured cap;
/// the Unbounded default always admits.
/// </summary>
public sealed class CapacityProbeTests
{
    private static ResourceLimits SandboxSize() =>
        new(cpuRequest: "500m", cpuLimit: "1000m", memoryRequest: "1Gi", memoryLimit: "2Gi");

    // p0320b: single-sandbox run without an orchestrator pod (the in-process shape).
    private static RunFootprint Footprint() => new(Orchestrator: null, [SandboxSize()]);

    // ---- Kubernetes: pure Evaluate over a ResourceQuota ----

    [Fact]
    public void Evaluate_NoQuotas_Admits()
    {
        KubernetesCapacityProbe.Evaluate(quotas: null, Footprint(), "ns")
            .Admitted.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_RoomAvailable_Admits()
    {
        // hard 4 CPU / used 1 CPU -> 3 CPU free, footprint needs 0.5.
        var quota = Quota("compute", hard: new() { ["requests.cpu"] = "4", ["requests.memory"] = "8Gi" },
                                   used: new() { ["requests.cpu"] = "1", ["requests.memory"] = "2Gi" });

        KubernetesCapacityProbe.Evaluate(new List<V1ResourceQuota> { quota }, Footprint(), "ns")
            .Admitted.Should().BeTrue();
    }

    [Fact]
    public void Evaluate_QuotaHardReached_ReportsNoCapacity()
    {
        // hard 1 CPU / used 1 CPU -> 0 free, footprint needs 0.5 -> deny.
        var quota = Quota("compute", hard: new() { ["requests.cpu"] = "1" },
                                   used: new() { ["requests.cpu"] = "1" });

        var decision = KubernetesCapacityProbe.Evaluate(
            new List<V1ResourceQuota> { quota }, Footprint(), "ns");

        decision.Admitted.Should().BeFalse();
        decision.Reason.Should().Contain("requests.cpu");
    }

    [Fact]
    public void Evaluate_PodCountExhausted_ReportsNoCapacity()
    {
        var quota = Quota("pods", hard: new() { ["pods"] = "2" }, used: new() { ["pods"] = "2" });

        KubernetesCapacityProbe.Evaluate(new List<V1ResourceQuota> { quota }, Footprint(), "ns")
            .Admitted.Should().BeFalse();
    }

    // ---- Kubernetes: p0320b full-run footprint math ----

    [Fact]
    public void K8sProbe_Evaluate_SumsOrchestratorPlusThreeSandboxes()
    {
        // Room for 3 CPU of requests; orchestrator 500m + 3 sandboxes x 500m = 2 CPU
        // fits, but a 4th sandbox (2.5 CPU total sandboxes) would not.
        var quota = Quota("compute", hard: new() { ["requests.cpu"] = "3" },
                                   used: new() { ["requests.cpu"] = "1" });
        var orchestrator = new ResourceLimits("500m", "1", "256Mi", "512Mi");

        var fits = new RunFootprint(orchestrator, [SandboxSize(), SandboxSize(), SandboxSize()]);
        KubernetesCapacityProbe.Evaluate(new List<V1ResourceQuota> { quota }, fits, "ns")
            .Admitted.Should().BeTrue();

        var tooBig = new RunFootprint(
            orchestrator, [SandboxSize(), SandboxSize(), SandboxSize(), SandboxSize()]);
        var decision = KubernetesCapacityProbe.Evaluate(new List<V1ResourceQuota> { quota }, tooBig, "ns");
        decision.Admitted.Should().BeFalse();
        decision.Reason.Should().Contain("requests.cpu");
    }

    [Fact]
    public void K8sProbe_Evaluate_PodsCountRequiresRoomForAllPods()
    {
        // 3 pod slots free, but orchestrator + 3 sandboxes = 4 pods → deny.
        var quota = Quota("pods", hard: new() { ["pods"] = "5" }, used: new() { ["pods"] = "2" });
        var orchestrator = new ResourceLimits("100m", "500m", "128Mi", "256Mi");
        var run = new RunFootprint(orchestrator, [SandboxSize(), SandboxSize(), SandboxSize()]);

        var decision = KubernetesCapacityProbe.Evaluate(new List<V1ResourceQuota> { quota }, run, "ns");

        decision.Admitted.Should().BeFalse();
        decision.Reason.Should().Contain("pods");
    }

    // ---- Kubernetes: p0332 requests-only quota shape ----

    [Fact]
    public void Probe_RequestsOnlyQuota_ComparesRequestsAndPods_IgnoresLimits()
    {
        // p0332 PIN: the cost-honest quota shape is requests.cpu / requests.memory /
        // pods (limits.* removed). Evaluate compares only keys PRESENT in
        // Status.Hard, and RequiredAmounts emits requests.* per pod — so a pod
        // whose LIMITS dwarf the quota must still be admitted as long as its
        // REQUESTS fit. If this ever denies on limits, requests-based quotas are
        // silently broken.
        var quota = Quota("capacity",
            hard: new() { ["requests.cpu"] = "2", ["requests.memory"] = "4Gi", ["pods"] = "5" },
            used: new() { ["requests.cpu"] = "1", ["requests.memory"] = "2Gi", ["pods"] = "2" });
        // Limits far beyond the quota's total hard values — they would deny
        // instantly if limits were counted against a requests-only quota.
        var hugeLimits = new ResourceLimits("100m", "8", "1Gi", "64Gi");

        KubernetesCapacityProbe.Evaluate(
                new List<V1ResourceQuota> { quota }, new RunFootprint(null, [hugeLimits]), "ns")
            .Admitted.Should().BeTrue("limits.* keys are absent from the quota, so limits must not count");

        // Requests still enforce: 3 x 1Gi = 3Gi > the 2Gi free requests.memory.
        var tooManyRequests = new RunFootprint(null, [hugeLimits, hugeLimits, hugeLimits]);
        var denied = KubernetesCapacityProbe.Evaluate(
            new List<V1ResourceQuota> { quota }, tooManyRequests, "ns");
        denied.Admitted.Should().BeFalse();
        denied.Reason.Should().Contain("requests.memory");

        // And pods stays the deterministic backpressure knob of the shape:
        // 3 pod slots free, but tiny-request pods still count 1 slot each.
        var podQuota = Quota("capacity",
            hard: new() { ["requests.cpu"] = "2", ["requests.memory"] = "4Gi", ["pods"] = "5" },
            used: new() { ["requests.cpu"] = "0", ["requests.memory"] = "0", ["pods"] = "2" });
        var tiny = new ResourceLimits("100m", "8", "128Mi", "64Gi");
        var fourPods = new RunFootprint(tiny, [tiny, tiny, tiny]);
        var podDenied = KubernetesCapacityProbe.Evaluate(
            new List<V1ResourceQuota> { podQuota }, fourPods, "ns");
        podDenied.Admitted.Should().BeFalse();
        podDenied.Reason.Should().Contain("pods");
    }

    // ---- Kubernetes: quota-rejection message mapping at the factory boundary ----

    [Fact]
    public void TryMapQuotaMessage_ExceededQuota_MapsWithResource()
    {
        const string msg = "pods \"agentsmith-sandbox-abc\" is forbidden: exceeded quota: compute, "
                         + "requested: requests.cpu=1, used: requests.cpu=6, limited: requests.cpu=6";

        var mapped = KubernetesSandboxFactory.TryMapQuotaMessage(msg, "agentsmith", inner: null, out var ex);

        mapped.Should().BeTrue();
        ex!.Scope.Should().Be("agentsmith");
        ex.ExhaustedResource.Should().Be("requests.cpu");
    }

    [Fact]
    public void TryMapQuotaMessage_RbacForbidden_ReturnsFalse()
    {
        const string msg = "pods is forbidden: User \"system:serviceaccount:ns:sa\" cannot create "
                         + "resource \"pods\" in API group \"\" in the namespace \"ns\"";

        KubernetesSandboxFactory.TryMapQuotaMessage(msg, "ns", inner: null, out var ex)
            .Should().BeFalse();
        ex.Should().BeNull();
    }

    // ---- Docker: configured concurrent-sandbox cap ----

    [Fact]
    public async Task DockerCapacityProbe_AtCap_ReportsNoCapacity()
    {
        var docker = DockerWithRunningSandboxes(count: 2);
        var probe = new DockerCapacityProbe(
            docker.Object, new DockerSandboxOptions { MaxConcurrentSandboxes = 2 },
            NullLogger<DockerCapacityProbe>.Instance);

        (await probe.HasCapacityAsync(Footprint(), CancellationToken.None))
            .Admitted.Should().BeFalse();
    }

    [Fact]
    public async Task DockerCapacityProbe_BelowCap_Admits()
    {
        var docker = DockerWithRunningSandboxes(count: 1);
        var probe = new DockerCapacityProbe(
            docker.Object, new DockerSandboxOptions { MaxConcurrentSandboxes = 2 },
            NullLogger<DockerCapacityProbe>.Instance);

        (await probe.HasCapacityAsync(Footprint(), CancellationToken.None))
            .Admitted.Should().BeTrue();
    }

    [Fact]
    public async Task DockerProbe_CountsAllSandboxesOfTheRun()
    {
        // p0320b: 1 running, cap 3 — a run needing 2 sandboxes fits (1+2<=3), a run
        // needing 3 does not (1+3>3).
        var docker = DockerWithRunningSandboxes(count: 1);
        var probe = new DockerCapacityProbe(
            docker.Object, new DockerSandboxOptions { MaxConcurrentSandboxes = 3 },
            NullLogger<DockerCapacityProbe>.Instance);

        var twoRepoRun = new RunFootprint(null, [SandboxSize(), SandboxSize()]);
        (await probe.HasCapacityAsync(twoRepoRun, CancellationToken.None))
            .Admitted.Should().BeTrue();

        var threeRepoRun = new RunFootprint(null, [SandboxSize(), SandboxSize(), SandboxSize()]);
        (await probe.HasCapacityAsync(threeRepoRun, CancellationToken.None))
            .Admitted.Should().BeFalse();
    }

    [Fact]
    public async Task DockerCapacityProbe_CapZero_AlwaysAdmits()
    {
        // No Docker call should even be needed — cap 0 short-circuits to admit.
        var docker = new Mock<IDockerClient>(MockBehavior.Strict);
        var probe = new DockerCapacityProbe(
            docker.Object, new DockerSandboxOptions { MaxConcurrentSandboxes = 0 },
            NullLogger<DockerCapacityProbe>.Instance);

        (await probe.HasCapacityAsync(Footprint(), CancellationToken.None))
            .Admitted.Should().BeTrue();
    }

    // ---- Unbounded default ----

    [Fact]
    public async Task UnboundedCapacityProbe_Always_Admits()
    {
        (await new UnboundedCapacityProbe().HasCapacityAsync(Footprint(), CancellationToken.None))
            .Admitted.Should().BeTrue();
    }

    // ---- helpers ----

    private static V1ResourceQuota Quota(
        string name, Dictionary<string, string> hard, Dictionary<string, string> used) =>
        new()
        {
            Metadata = new V1ObjectMeta { Name = name },
            Status = new V1ResourceQuotaStatus
            {
                Hard = hard.ToDictionary(kv => kv.Key, kv => new ResourceQuantity(kv.Value)),
                Used = used.ToDictionary(kv => kv.Key, kv => new ResourceQuantity(kv.Value)),
            },
        };

    private static Mock<IDockerClient> DockerWithRunningSandboxes(int count)
    {
        var containers = Enumerable.Range(0, count)
            .Select(i => new ContainerListResponse
            {
                ID = $"c{i}",
                Labels = new Dictionary<string, string> { [DockerContainerSpecBuilder.JobIdLabel] = $"job{i}" },
            })
            .ToList();

        var ops = new Mock<IContainerOperations>();
        ops.Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<ContainerListResponse>)containers);

        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Containers).Returns(ops.Object);
        return docker;
    }
}
