using AgentSmith.Application.Services.Claim;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestSupport;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0465: a second server on the same Docker daemon used to force-remove the first
/// one's live sandboxes — its own reaper judged them against ITS active-run set and
/// found them orphaned. Ownership is now a term of the QUERY, so a foreign container
/// is never a candidate. The fake daemon applies the label filters exactly as Docker
/// does, which is what makes "never listed" provable rather than asserted.
/// </summary>
public sealed class SandboxOrphanReaperOwnershipTests
{
    private static readonly SandboxOwnerIdentity Owner = new("store-0123456789abcdef");
    private static readonly SandboxOwnerIdentity Foreign = new("store-fedcba9876543210");
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly TimeSpan PastTheAgeRail = SandboxOrphanReaper.MinContainerAge * 2;

    [Fact]
    public void Query_AsksOnlyForTheContainersOfItsOwnLivenessStore()
    {
        var parameters = new DockerSandboxQuery(Owner).Owned(includeStopped: true);

        parameters.Filters["label"].Keys.Should().BeEquivalentTo(
            DockerContainerSpecBuilder.JobIdLabel,
            $"{DockerContainerSpecBuilder.OwnerLabel}={Owner.Value}");
    }

    [Fact]
    public async Task ForeignContainer_IsNotListedAndIsNotRemoved_ThoughBothRailsAreSatisfied()
    {
        var daemon = new FakeDockerDaemon(
            Sandbox("foreign-1", Foreign, runId: "run-of-the-other-server", age: PastTheAgeRail));
        var reaper = NewReaper(daemon);

        await reaper.ScanOnceAsync(CancellationToken.None); // spends the one-time unowned sweep
        daemon.Listed.Clear();
        await reaper.ScanOnceAsync(CancellationToken.None);

        daemon.Listed.Should().NotContain("foreign-1",
            "the ownership term belongs in the query — no rail should have to save a foreign sandbox");
        daemon.Removed.Should().BeEmpty(
            "a sandbox of another liveness store is not this server's to remove");
    }

    [Fact]
    public async Task OwnContainer_WithNoLiveRun_IsStillReaped_TheFixDoesNotDisableTheFeature()
    {
        var daemon = new FakeDockerDaemon(
            Sandbox("mine-1", Owner, runId: "run-long-dead", age: PastTheAgeRail));

        await NewReaper(daemon).ScanOnceAsync(CancellationToken.None);

        daemon.Removed.Should().ContainSingle().Which.Should().Be("mine-1");
    }

    [Fact]
    public async Task UnownedContainer_FromABinaryBeforeThisPhase_IsSweptOnceThenNeverListedAgain()
    {
        var daemon = new FakeDockerDaemon(
            SandboxWithoutOwner("legacy-1", runId: "run-long-dead", age: PastTheAgeRail),
            SandboxWithoutOwner("legacy-2", runId: "run-long-dead", age: TimeSpan.FromSeconds(5)));
        var reaper = NewReaper(daemon);

        await reaper.ScanOnceAsync(CancellationToken.None);
        daemon.Removed.Should().ContainSingle().Which.Should().Be("legacy-1");

        daemon.Listed.Clear();
        await reaper.ScanOnceAsync(CancellationToken.None);
        daemon.Listed.Should().NotContain("legacy-2", "the pre-phase sweep runs once per process");
    }

    [Fact]
    public void Judge_LiveRun_IsSpared_AndYoungContainer_IsSpared()
    {
        var containers = new[]
        {
            Sandbox("live", Owner, "run-alive", PastTheAgeRail),
            Sandbox("young", Owner, "run-unknown", TimeSpan.FromSeconds(5)),
            Sandbox("dead", Owner, "run-gone", PastTheAgeRail)
        };

        var verdicts = SandboxOrphanReaper.Judge(
            containers, new HashSet<string> { "run-alive" }, SandboxOrphanReaper.MinContainerAge, Now);

        verdicts.Single(v => v.ContainerId == "live").Outcome.Should().Be(SandboxReapOutcome.RunIsLive);
        verdicts.Single(v => v.ContainerId == "young").Outcome.Should().Be(SandboxReapOutcome.TooYoung);
        verdicts.Single(v => v.ContainerId == "dead").Outcome.Should().Be(SandboxReapOutcome.Orphan);
    }

    private static SandboxOrphanReaper NewReaper(FakeDockerDaemon daemon) =>
        new(daemon.Client,
            new DockerSandboxQuery(Owner),
            new LiveRunSetReader(
                InMemoryRedis.Connection(), new NoOpActiveRunLease(), NullLogger<LiveRunSetReader>.Instance),
            new DockerSandboxRemover(daemon.Client, NullLogger<DockerSandboxRemover>.Instance),
            NullLogger<SandboxOrphanReaper>.Instance);

    private static ContainerListResponse Sandbox(
        string id, SandboxOwnerIdentity owner, string runId, TimeSpan age)
    {
        var container = SandboxWithoutOwner(id, runId, age);
        container.Labels[DockerContainerSpecBuilder.OwnerLabel] = owner.Value;
        return container;
    }

    private static ContainerListResponse SandboxWithoutOwner(string id, string runId, TimeSpan age) => new()
    {
        ID = id,
        Created = (Now - age).UtcDateTime,
        Labels = new Dictionary<string, string>
        {
            [DockerContainerSpecBuilder.JobIdLabel] = "job-" + id,
            [DockerContainerSpecBuilder.RunIdLabel] = runId
        }
    };
}
