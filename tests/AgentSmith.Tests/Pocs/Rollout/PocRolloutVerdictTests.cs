using AgentSmith.Application.Services.Pocs.Rollout;
using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Rollout;

/// <summary>2026-11-23-c81ec: a reading counts only once the live pod template carries THIS render; then Synced +
/// Healthy + replicas settles and Degraded fails.</summary>
public sealed class PocRolloutVerdictTests
{
    internal const string Digest = "sha256:1111111111111111111111111111111111111111111111111111111111111111";
    internal const string OldDigest = "sha256:0000000000000000000000000000000000000000000000000000000000000000";
    internal const string Image = "pocregistry.azurecr.io/pocs/todo:abc@" + Digest;
    internal const string OldImage = "pocregistry.azurecr.io/pocs/todo:old@" + OldDigest;

    private static readonly ArgoApplicationState Healthy = new("Synced", "Healthy", null, "Succeeded", null);
    private readonly PocRolloutVerdict _verdict = new();

    internal static PocRolloutExpected Expected(int replicas = 1, string digest = Digest, string checksum = "sha256:c1") =>
        new("poc-todo", "poc-todo", false, "pocregistry.azurecr.io", "pocs/todo", digest, replicas, checksum);

    internal static ArgoLiveDeployment Live(string image = Image, string checksum = "sha256:c1", int replicas = 1, long generation = 2,
        long observed = 2, int ready = 1, int updated = 1) => new(image, checksum, replicas, generation, observed, ready, updated);

    [Fact]
    public void Judge_ThisRender_SyncedHealthyAndReplicas_Settles() =>
        _verdict.Judge(Expected(), Healthy, Live()).Kind.Should().Be(PocRolloutReadingKind.Settled);

    [Fact]
    public void Judge_SyncedHealthyWithTheOldTemplateImage_KeepsWaiting()
    {
        var reading = _verdict.Judge(Expected(), Healthy, Live(image: OldImage));

        reading.Kind.Should().Be(PocRolloutReadingKind.Waiting);
        reading.Line.Should().Contain("template image is another").And.Contain("not this render yet");
    }

    [Fact]
    public void Judge_AnotherPocsNewerSyncRevision_DoesNotMatter() =>
        _verdict.Judge(Expected(), Healthy with { }, Live()).Kind.Should().Be(PocRolloutReadingKind.Settled,
            "the verdict never compares Argo's sync revision with the run's commit");

    [Fact]
    public void Judge_DesiredZero_SettlesWithNoReadyReplicasAndNoPodImages() =>
        _verdict.Judge(Expected(replicas: 0), Healthy, Live(replicas: 0, ready: 0, updated: 0)).Kind.Should().Be(PocRolloutReadingKind.Settled);

    [Fact]
    public void Judge_Absent_SettlesOnlyOnceTheApplicationIsGone()
    {
        var gone = PocRolloutExpected.Gone("poc-todo");

        _verdict.Judge(gone, null, null).Kind.Should().Be(PocRolloutReadingKind.Settled);
        _verdict.Judge(gone, Healthy, null).Kind.Should().Be(PocRolloutReadingKind.Waiting);
    }

    [Fact]
    public void Judge_ARollbackToADigestAnOldReplicaSetLists_DoesNotSettleUntilTheTemplateNamesIt()
    {
        var rollback = Expected(digest: OldDigest);

        _verdict.Judge(rollback, Healthy, Live(image: Image)).Kind.Should().Be(PocRolloutReadingKind.Waiting);
        _verdict.Judge(rollback, Healthy, Live(image: OldImage)).Kind.Should().Be(PocRolloutReadingKind.Settled);
    }

    [Fact]
    public void Judge_AChangedChecksumWithTheSameDigest_DoesNotSettleUntilTheTemplateCarriesIt()
    {
        var apply = Expected(checksum: "sha256:c2");

        _verdict.Judge(apply, Healthy, Live(checksum: "sha256:c1")).Line.Should().Contain("checksum differs");
        _verdict.Judge(apply, Healthy, Live(checksum: "sha256:c1")).Kind.Should().Be(PocRolloutReadingKind.Waiting);
        _verdict.Judge(apply, Healthy, Live(checksum: "sha256:c2")).Kind.Should().Be(PocRolloutReadingKind.Settled);
    }

    [Fact]
    public void Judge_ObservedGenerationBelowGeneration_Waits() =>
        _verdict.Judge(Expected(), Healthy, Live(generation: 3, observed: 2)).Kind.Should().Be(PocRolloutReadingKind.Waiting);

    [Fact]
    public void Judge_DegradedLeftByThePreviousRender_DoesNotFailTheNewRun()
    {
        var degraded = new ArgoApplicationState("Synced", "Degraded", "old pods crash", "Failed", null);

        _verdict.Judge(Expected(), degraded, Live(image: OldImage)).Kind.Should().Be(PocRolloutReadingKind.Waiting);
    }

    [Fact]
    public void Judge_DegradedOnThisRender_FailsNamingArgosMessage()
    {
        var degraded = new ArgoApplicationState("Synced", "Degraded", "Deployment app exceeded its progress deadline", null, null);

        var reading = _verdict.Judge(Expected(), degraded, Live(ready: 0));

        reading.Kind.Should().Be(PocRolloutReadingKind.Failed);
        reading.Message.Should().Be("Deployment app exceeded its progress deadline");
    }

    [Fact]
    public void Judge_ProgressingOnThisRender_Waits_AndTheLineNamesEveryPart()
    {
        var reading = _verdict.Judge(Expected(), new ArgoApplicationState("Synced", "Progressing", null, null, null), Live(ready: 0));

        reading.Kind.Should().Be(PocRolloutReadingKind.Waiting);
        reading.Line.Should().Contain("sync Synced").And.Contain("health Progressing").And.Contain("template image is this render's")
            .And.Contain("checksum matches").And.Contain("generation 2/2").And.Contain("0 ready / 1 updated / 1 of 1");
    }

    [Fact]
    public void Judge_AnImageOfAnotherRepository_IsNotThisRender() =>
        _verdict.Judge(Expected(), Healthy, Live(image: "pocregistry.azurecr.io/pocs/todo2:abc@" + Digest)).Kind
            .Should().Be(PocRolloutReadingKind.Waiting);
}
