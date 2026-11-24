using AgentSmith.Contracts.Pocs;
using AgentSmith.Tests.Pocs.GitOps;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Lifetime;

/// <summary>2026-11-23-c81ee: a new upload to a stopped PoC starts it (round 4), except to an expired one — poc-deploy keeps
/// its row's replicas and says why.</summary>
public sealed class PocDeployExpiryTests
{
    private readonly GitOpsCommitRig _rig = new();

    private void StoppedRow(long? expiresAt)
    {
        _rig.Store.Poc = _rig.Store.Poc with
        {
            Replicas = 0, State = PocStates.Stopped, Revision = 1, ImageDigest = PocRenderRig.Digest, Lifetime = new PocLifetimeFacts(_rig.Clock.Now.AddDays(-100), expiresAt, null, 0),
        };
    }

    [Fact]
    public async Task Deploy_ToAStoppedPocNotExpired_CommitsOneReplica()
    {
        StoppedRow(_rig.Clock.Now.AddDays(30).UtcTicks);

        var result = await _rig.RunAsync(PocRenderRig.Seal());

        result.IsSuccess.Should().BeTrue(result.Message);
        _rig.Branch.Files["deploy/shop/deployment.yaml"].Should().Contain("  replicas: 1\n");
        _rig.Store.Poc.Replicas.Should().Be(1);
    }

    [Fact]
    public async Task Deploy_ToAnExpiredStoppedPoc_CommitsZeroReplicas_AndNamesTheExpiryAndExtend()
    {
        StoppedRow(null);

        var result = await _rig.RunAsync(PocRenderRig.Seal());

        result.IsSuccess.Should().BeTrue(result.Message);
        result.Message.Should().StartWith($"'shop' expired on {_rig.Clock.Now.AddDays(-10):yyyy-MM-dd}: revision 2 is deployed stopped; extend it, then start it.");
        _rig.Branch.Files["deploy/shop/deployment.yaml"].Should().Contain("  replicas: 0\n");
        _rig.Store.Poc.Replicas.Should().Be(0);
    }
}
