using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Tests.Pocs.GitOps;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Acts;

/// <summary>2026-11-23-c81ed: an act's step and the GitOps commit together — what lands in the deploy folder, what the row
/// holds after it, and which event is published.</summary>
public sealed class PocActCommitTests
{
    private readonly PocActRig _rig = new();

    private string Deployment => _rig.Commit.Branch.Files["deploy/shop/deployment.yaml"];

    [Fact]
    public async Task Stop_CommitsZeroReplicasAsAnInteger_AndASecondStopCommitsNothing()
    {
        await _rig.DeployAsync();

        var (act, commit, _) = await _rig.RunAsync(PipelinePresets.PocStopName);

        act.IsSuccess.Should().BeTrue(act.Message);
        commit!.IsSuccess.Should().BeTrue(commit.Message);
        Deployment.Should().Contain("  replicas: 0\n");
        _rig.Commit.Store.Poc.Replicas.Should().Be(0);
        _rig.Commit.Branch.Writes.Should().HaveCount(2);
        _rig.Commit.Acted.Should().ContainSingle().Which.Should().Match<AgentSmith.Contracts.Events.PocActedEvent>(e =>
            e.Act == PocActKinds.Stop && e.CommitSha == "sha2" && e.Revision == 2);
        _rig.Commit.Published.Should().ContainSingle("the deploy published PocDeployedEvent, the act does not");

        var (_, again, _) = await _rig.RunAsync(PipelinePresets.PocStopName, runId: "act-2");

        again!.Message.Should().Contain("already holds revision 2");
        _rig.Commit.Branch.Writes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Rollback_CommitsTheOlderDigest_AndTheRowFollowsAfterTheCommit()
    {
        await _rig.DeployAsync();

        var (act, commit, _) = await _rig.RunAsync(PipelinePresets.PocRollbackName, revision: 1);

        act.IsSuccess.Should().BeTrue(act.Message);
        commit!.IsSuccess.Should().BeTrue(commit.Message);
        Deployment.Should().Contain("@" + PocActRig.OlderDigest).And.NotContain(PocRenderRig.Digest);
        (_rig.Commit.Store.Poc.Revision, _rig.Commit.Store.Poc.ImageDigest).Should().Be((1, PocActRig.OlderDigest));
    }

    [Fact]
    public async Task Rollback_ToAnImageTheRegistryLost_IsRefusedNamingTheDigest()
    {
        await _rig.DeployAsync();
        _rig.ImageExists = false;

        var (act, commit, _) = await _rig.RunAsync(PipelinePresets.PocRollbackName, revision: 1);

        act.IsSuccess.Should().BeFalse();
        act.Message.Should().Contain(PocActRig.OlderDigest).And.Contain("Revision 1");
        commit.Should().BeNull();
        _rig.Commit.Store.Poc.Revision.Should().Be(2);
    }

    [Fact]
    public async Task Apply_OnAStoppedPoc_KeepsZeroReplicas_AndSetsTheReadinessSeed()
    {
        await _rig.DeployAsync();
        await _rig.RunAsync(PipelinePresets.PocStopName);
        _rig.Acts.State = PocStates.Stopped;

        var (_, commit, _) = await _rig.RunAsync(PipelinePresets.PocApplyName, readiness: "/healthz", runId: "act-2");

        commit!.IsSuccess.Should().BeTrue(commit.Message);
        Deployment.Should().Contain("  replicas: 0\n").And.Contain("/healthz");
        (_rig.Commit.Store.Poc.Replicas, _rig.Commit.Store.Poc.ReadinessPath).Should().Be((0, "/healthz"));
        _rig.Commit.Acted.Last().Reason.Should().Be("applied");
    }

    [Fact]
    public async Task Delete_RemovesEveryFileUnderTheFolderInOneCommit_AndNullsTheRow_AndARetryCommitsNothing()
    {
        await _rig.DeployAsync();
        _rig.Commit.Branch.Files["deploy/shop/extra/operator.yaml"] = "kind: ConfigMap\n";
        _rig.Commit.Branch.Files["pocs/poc-ann/shop/app.py"] = "print('hi')\n";

        var (act, commit, pipeline) = await _rig.RunAsync(PipelinePresets.PocDeleteName);

        act.IsSuccess.Should().BeTrue(act.Message);
        commit!.IsSuccess.Should().BeTrue(commit.Message);
        _rig.Commit.Branch.Writes.Should().HaveCount(2);
        _rig.Commit.Branch.Writes[1].Should().OnlyContain(f => f.Delete).And.Contain(f => f.Path == "deploy/shop/extra/operator.yaml");
        _rig.Commit.Branch.Files.Keys.Should().NotContain(k => k.StartsWith("deploy/shop/")).And.Contain("pocs/poc-ann/shop/app.py");
        (_rig.Commit.Store.Poc.Revision, _rig.Commit.Store.Poc.ImageDigest, _rig.Commit.Store.Poc.SealFingerprint).Should().Be((null, null, null));
        pipeline.TryGet<bool>(ContextKeys.PocRolloutAbsent, out var absent).Should().BeTrue();
        absent.Should().BeTrue();
        _rig.Commit.Acted.Should().ContainSingle().Which.Act.Should().Be(PocActKinds.Delete);

        var (retryAct, retry, _) = await _rig.RunAsync(PipelinePresets.PocDeleteName, runId: "act-2");

        retryAct.IsSuccess.Should().BeTrue(retryAct.Message);
        retry!.Message.Should().Contain("already gone");
        _rig.Commit.Branch.Writes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Delete_ATruncatedListing_FailsNamingMaxFolderFiles()
    {
        await _rig.DeployAsync();
        _rig.Commit.Branch.TruncateListings = true;

        var (_, commit, _) = await _rig.RunAsync(PipelinePresets.PocDeleteName);

        commit!.IsSuccess.Should().BeFalse();
        commit.Message.Should().Contain("pocs.gitops.max_folder_files");
        _rig.Commit.Store.Poc.Revision.Should().Be(2);
    }

    [Fact]
    public async Task DeletedThenReusedName_ARollbackOfTheOldPoc_IsRefused_AndTheNewFolderIsUntouched()
    {
        await _rig.DeployAsync();
        await _rig.RunAsync(PipelinePresets.PocDeleteName);
        _rig.Acts.State = PocStates.Deleted;
        _rig.Commit.Branch.Files["deploy/shop/deployment.yaml"] = "kind: Deployment # the new PoC B\n";
        var writes = _rig.Commit.Branch.Writes.Count;

        var (rollback, commit, _) = await _rig.RunAsync(PipelinePresets.PocRollbackName, revision: 1, runId: "act-3");
        var (apply, _, _) = await _rig.RunAsync(PipelinePresets.PocApplyName, runId: "act-4");

        rollback.IsSuccess.Should().BeFalse();
        rollback.Message.Should().Contain("is deleted");
        apply.IsSuccess.Should().BeFalse();
        commit.Should().BeNull();
        _rig.Commit.Branch.Writes.Should().HaveCount(writes);
        Deployment.Should().Be("kind: Deployment # the new PoC B\n");
    }

    // Review finding: a poc-deploy (an overrule's, a replace's) must not render a deleted PoC into a folder whose name a
    // new PoC may own, nor re-render a folder a pending delete removed.
    [Theory]
    [InlineData(PocStates.Deleted, 2)]
    [InlineData(PocStates.Ready, null)]
    public async Task Deploy_OfADeletedOrDeletePendingPoc_RendersNothing(string state, int? revision)
    {
        _rig.Commit.Store.Poc = _rig.Commit.Store.Poc with { State = state, Revision = revision };

        var result = await _rig.Commit.RunAsync(PocRenderRig.Seal());

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("nothing is rendered");
        _rig.Commit.Branch.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task Act_InAPresetThatIsNoAct_Fails()
    {
        var (act, _, _) = await _rig.RunAsync(PipelinePresets.PocDeployName);

        act.IsSuccess.Should().BeFalse();
        act.Message.Should().Contain("poc-deploy");
    }
}
