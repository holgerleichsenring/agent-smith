using AgentSmith.Contracts.Commands;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: the GitOps commit — once, recorded after it landed, idempotent, re-planned on a conflict,
/// published on every good end of a poc-deploy run.</summary>
public sealed class GitOpsCommitHandlerTests
{
    private readonly GitOpsCommitRig _rig = new();

    [Fact]
    public async Task Execute_CommitsTheFolderOnce_AndRecordsTheDesiredStateAndFingerprintAfterIt()
    {
        var result = await _rig.RunAsync(PocRenderRig.Seal());

        result.IsSuccess.Should().BeTrue(result.Message);
        _rig.Branch.Writes.Should().ContainSingle();
        _rig.Branch.Files.Keys.Where(k => k.StartsWith("deploy/shop/")).Should().BeEquivalentTo(
            new[] { "namespace", "resourcequota", "deployment", "service", "ingress", "networkpolicy", "configmap", "poc-pull.sealed" }
                .Select(n => $"deploy/shop/{n}.yaml"));
        _rig.Branch.Files["deploy/shop/deployment.yaml"].Should().Contain(PocRenderRig.Digest);
        (_rig.Store.Poc.Revision, _rig.Store.Poc.ImageDigest, _rig.Store.Poc.Replicas, _rig.Store.Poc.SealFingerprint)
            .Should().Be((2, PocRenderRig.Digest, 1, PocRenderRig.Seal().SealFingerprint));
        _rig.Published.Should().ContainSingle().Which.CommitSha.Should().Be("sha1");
    }

    [Fact]
    public async Task Execute_ARetry_CommitsNothing_AndPublishesAgain()
    {
        await _rig.RunAsync(PocRenderRig.Seal());

        var retry = await _rig.RunAsync(PocRenderRig.Seal(unchanged: true));

        retry.IsSuccess.Should().BeTrue(retry.Message);
        retry.Message.Should().Contain("already holds revision 2");
        _rig.Branch.Writes.Should().ContainSingle();
        _rig.Published.Should().HaveCount(2).And.OnlyContain(e => e.RunId == "run-1");
        _rig.Published[1].CommitSha.Should().BeNull();
        _rig.Store.Records.Should().Be(2);
    }

    [Fact]
    public async Task Execute_AConflict_RePlansFromTheNewHead()
    {
        _rig.Branch.ConflictsLeft = 1;
        _rig.Branch.OnConflict = () => _rig.Branch.Files["deploy/shop/stray.yaml"] = "kind: Stray\n";

        var result = await _rig.RunAsync(PocRenderRig.Seal());

        result.IsSuccess.Should().BeTrue(result.Message);
        _rig.Branch.Writes.Should().ContainSingle().Which.Should().Contain(f => f.Delete && f.Path == "deploy/shop/stray.yaml");
        _rig.Branch.Files.Should().NotContainKey("deploy/shop/stray.yaml");
    }

    [Fact]
    public async Task Execute_AFailedWrite_LeavesTheRowUnchanged()
    {
        _rig.Branch.FailWrites = true;

        var result = await _rig.RunAsync(PocRenderRig.Seal());

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("the host refused the push");
        _rig.Store.Records.Should().Be(0);
        _rig.Store.Poc.Revision.Should().BeNull();
        _rig.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_ATruncatedDeployFolderListing_FailsNamingMaxFolderFiles()
    {
        _rig.Pocs.Gitops.MaxFolderFiles = 8;
        for (var i = 0; i < 9; i++) _rig.Branch.Files[$"deploy/shop/extra{i}.yaml"] = "x: 1\n";

        var result = await _rig.RunAsync(PocRenderRig.Seal());

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("deploy/shop/").And.Contain("more than 8 files").And.Contain("pocs.gitops.max_folder_files");
    }

    [Fact]
    public async Task Execute_AStoppedPoc_CommitsTheNewRevisionWithOneReplica()
    {
        _rig.Store.Poc = _rig.Store.Poc with { Replicas = 0, Revision = 1 };

        await _rig.RunAsync(PocRenderRig.Seal());

        _rig.Branch.Files["deploy/shop/deployment.yaml"].Should().Contain("  replicas: 1\n");
        _rig.Store.Poc.Replicas.Should().Be(1);
    }

    [Fact]
    public async Task Execute_WithoutASeal_IsRefusedNamingTheSealStep()
    {
        var result = await _rig.RunAsync(null);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("the seal step did not run");
        _rig.Branch.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_AnUnchangedSealWhoseFileTheBranchLost_ClearsTheFingerprintAndFailsNamingIt()
    {
        _rig.Store.Poc = _rig.Store.Poc with { SealFingerprint = PocRenderRig.Seal().SealFingerprint };

        var result = await _rig.RunAsync(PocRenderRig.Seal(unchanged: true));

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("poc-pull.sealed.yaml");
        _rig.Store.Poc.SealFingerprint.Should().BeNull();
        _rig.Branch.Writes.Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_SetsTheCommitSha()
    {
        var context = _rig.Context(PocRenderRig.Seal());

        await _rig.Handler().ExecuteAsync(context, default);

        context.Pipeline.Get<string>(ContextKeys.PocCommitSha).Should().Be("sha1");
    }
}
