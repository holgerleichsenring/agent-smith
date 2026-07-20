using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using k8s.Models;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0355: the corpse-pod reaper deletes sandbox pods whose owning run is not live.
/// The corpse DECISION is a pure function over the namespace's sandbox pods + the
/// live-run set, so it is proven here without a k8s client. A corpse is a pod that
/// is older than the spawn-window rail AND whose run-id label maps to no live run
/// (or that carries no run-id label at all — a pre-p0355 orphan).
/// </summary>
public sealed class KubernetesSandboxCorpseReaperTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-20T12:00:00Z");
    private static readonly TimeSpan MinAge = KubernetesSandboxCorpseReaper.MinPodAge;

    private static V1Pod Pod(string name, string? runId, TimeSpan age)
    {
        var labels = new Dictionary<string, string> { ["app"] = PodSpecBuilder.AppLabel };
        if (runId is not null) labels[PodSpecBuilder.RunIdLabel] = runId;
        return new V1Pod
        {
            Metadata = new V1ObjectMeta
            {
                Name = name,
                Labels = labels,
                CreationTimestamp = (Now - age).UtcDateTime,
            }
        };
    }

    private static ISet<string> Live(params string[] runIds) =>
        new HashSet<string>(runIds, StringComparer.Ordinal);

    [Fact]
    public void CorpsePod_NoLiveRun_DeletedByRunIdLabel()
    {
        var pods = new[] { Pod("agentsmith-sandbox-dead", "run-dead", TimeSpan.FromMinutes(30)) };

        var corpses = KubernetesSandboxCorpseReaper.SelectCorpses(pods, Live("run-alive"), MinAge, Now);

        corpses.Should().ContainSingle()
            .Which.Should().Be(("agentsmith-sandbox-dead", "run-dead"));
    }

    [Fact]
    public void LiveRunPod_IsSpared()
    {
        var pods = new[] { Pod("agentsmith-sandbox-live", "run-alive", TimeSpan.FromMinutes(30)) };

        KubernetesSandboxCorpseReaper.SelectCorpses(pods, Live("run-alive"), MinAge, Now)
            .Should().BeEmpty();
    }

    [Fact]
    public void YoungPod_IsSpared_SpawnWindowRace()
    {
        // A pod created seconds ago whose run id has not yet reached the live set.
        var pods = new[] { Pod("agentsmith-sandbox-young", "run-new", TimeSpan.FromSeconds(5)) };

        KubernetesSandboxCorpseReaper.SelectCorpses(pods, Live(), MinAge, Now)
            .Should().BeEmpty();
    }

    [Fact]
    public void OldPod_WithNoRunIdLabel_IsCorpse()
    {
        // A pre-p0355 sandbox pod with no owner signal, old enough to be an orphan.
        var pods = new[] { Pod("agentsmith-sandbox-legacy", runId: null, TimeSpan.FromHours(2)) };

        KubernetesSandboxCorpseReaper.SelectCorpses(pods, Live("run-alive"), MinAge, Now)
            .Should().ContainSingle().Which.PodName.Should().Be("agentsmith-sandbox-legacy");
    }

    [Fact]
    public void MixedPods_OnlyCorpsesSelected()
    {
        var pods = new[]
        {
            Pod("live", "run-alive", TimeSpan.FromMinutes(30)),
            Pod("corpse-1", "run-gone-1", TimeSpan.FromMinutes(30)),
            Pod("young", "run-pending", TimeSpan.FromSeconds(10)),
            Pod("corpse-2", "run-gone-2", TimeSpan.FromHours(1)),
        };

        var corpses = KubernetesSandboxCorpseReaper.SelectCorpses(pods, Live("run-alive"), MinAge, Now);

        corpses.Select(c => c.PodName).Should().BeEquivalentTo("corpse-1", "corpse-2");
    }
}
