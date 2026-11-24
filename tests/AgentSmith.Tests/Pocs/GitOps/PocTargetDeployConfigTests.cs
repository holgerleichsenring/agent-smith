using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.GitOps;

/// <summary>2026-11-23-c81eb: the target's ingress, auth, network, quota and container rules, the shared-repo rule and
/// pocs.gitops attached to the pocs block.</summary>
public sealed class PocTargetDeployConfigTests
{
    private static PocsConfig Sound() => PocsConfigTests.Sound();

    [Fact]
    public void Problems_EnabledWithoutAuthAnnotations_IsRefusedNamingKeyAndReason()
    {
        var pocs = Sound();
        pocs.Targets[0].Auth.IngressAnnotations.Clear();

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("pocs.targets[].auth.ingress_annotations").And.Contain("every PoC is behind auth");
    }

    [Fact]
    public void Problems_OffWithoutTheDeployKeys_Passes()
    {
        var pocs = Sound();
        pocs.Enabled = false;
        pocs.Targets[0].Auth.IngressAnnotations.Clear();
        pocs.Targets[0].Ingress.Class = null;

        pocs.Problems().Should().BeEmpty();
    }

    [Fact]
    public void Problems_BothProxyForms_IsRefused()
    {
        var pocs = Sound();
        pocs.Targets[0].Network.EgressProxy.Cidr = "10.0.0.0/24";

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("network.egress_proxy").And.Contain("exactly one form");
    }

    [Fact]
    public void Problems_QuotaMemoryEqualToTheLimit_IsRefusedNamingTheRollingUpdate()
    {
        var pocs = Sound();
        pocs.Targets[0].Quota.Memory = "512Mi";

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("quota.memory").And.Contain("a rolling update runs the old and the new pod side by side");
    }

    [Fact]
    public void Problems_EphemeralQuotaBelowTwiceTheLimit_IsRefused()
    {
        var pocs = Sound();
        pocs.Targets[0].Quota.EphemeralStorage = "300Mi";

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("quota.ephemeral_storage");
    }

    [Fact]
    public void Problems_AQuantityThatDoesNotParse_IsRefused()
    {
        var pocs = Sound();
        pocs.Targets[0].Container.CpuLimit = "half";

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("container.cpu_limit").And.Contain("'half'");
    }

    [Theory]
    [InlineData("500m", 0.5)]
    [InlineData("1", 1)]
    [InlineData("128Mi", 134217728)]
    [InlineData("1G", 1e9)]
    public void Quantity_Parses(string text, double expected) => ((double)PocQuantity.Parse(text)!).Should().Be(expected);

    [Fact]
    public void SharedRepo_ADeployRootEqualToAnotherTargetsRoot_IsABlockingFindingNamingBoth()
    {
        var pocs = Sound();
        pocs.Targets.Add(Second(t => t.DeployRoot = "pocs"));

        pocs.Problems().Should().Contain(p => p.Contains("deploy_root of target 'second'") && p.Contains("root of target 'main'"));
    }

    [Fact]
    public void SharedRepo_TwoTargetsSharingATemplatePath_IsABlockingFindingNamingBoth()
    {
        var pocs = Sound();
        pocs.Targets.Add(Second(t => t.TemplatePath = "poc-template"));

        pocs.Problems().Should().ContainSingle(p => p.Contains("template_path of targets 'main' and 'second'"));
    }

    [Fact]
    public void Gitops_AbsentReadsTheDefaults_AndZeroAttemptsIsRefused()
    {
        var pocs = Sound();
        (pocs.Gitops.CommitAttempts, pocs.Gitops.MaxFolderFiles).Should().Be((3, 500));

        pocs.Gitops.CommitAttempts = 0;

        pocs.Problems().Should().ContainSingle().Which.Should().Contain("pocs.gitops.commit_attempts");
    }

    private static PocTargetConfig Second(Action<PocTargetConfig> change)
    {
        var target = Sound().Targets[0];
        target.Name = "second";
        target.Root = "second-src";
        target.DeployRoot = "second-deploy";
        target.TemplatePath = "second-template";
        change(target);
        return target;
    }
}
