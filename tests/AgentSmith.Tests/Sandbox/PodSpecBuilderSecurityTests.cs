using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;
using k8s.Models;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-10-25-c5a7a: a sandbox pod runs as uid 1000 with no capabilities unless its project
/// opts into root; both profiles run RuntimeDefault seccomp, no escalation and no token.
/// </summary>
public sealed class PodSpecBuilderSecurityTests
{
    private static readonly PodSpecBuilder Builder =
        new(new SandboxPodLabels(new SandboxOwnerIdentity("store-test")), new PodSecurity());

    private static V1Pod Build(SandboxSecurityContext? profile) => Builder.Build(
        "p", "j", "redis:6379",
        new SandboxSpec("node:20", ResourceLimits.Default, "agent:1", SecurityContext: profile),
        owner: null);

    private static V1Container Toolchain(V1Pod pod) => pod.Spec.Containers.Single(c => c.Name == "toolchain");

    [Fact]
    public void PodSpecBuilder_Hardened_RunsAsUid1000DropsAllCapsRuntimeDefaultSeccomp()
    {
        var pod = Build(SandboxSecurityContext.Hardened);

        var podContext = pod.Spec.SecurityContext;
        podContext.RunAsNonRoot.Should().BeTrue();
        podContext.RunAsUser.Should().Be(1000);
        podContext.RunAsGroup.Should().Be(1000);
        podContext.FsGroup.Should().Be(1000);
        podContext.SeccompProfile.Type.Should().Be("RuntimeDefault");
        Toolchain(pod).SecurityContext.Capabilities.Drop.Should().Equal("ALL");
    }

    // 2026-10-25-ff3b: hardening off (and a spec with no profile) is the pod from before hardening.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PodSpecBuilder_HardeningOff_PodIsUnchangedFromBeforeHardening(bool explicitProfile)
    {
        var pod = Build(explicitProfile ? SandboxSecurityContext.Unconfined : null);

        pod.Spec.SecurityContext.FsGroup.Should().Be(1000);
        pod.Spec.SecurityContext.RunAsUser.Should().BeNull();
        pod.Spec.SecurityContext.SeccompProfile.Should().BeNull();
        pod.Spec.AutomountServiceAccountToken.Should().BeNull();
        pod.Spec.InitContainers.Concat(pod.Spec.Containers).Should().OnlyContain(c => c.SecurityContext == null);
        pod.Spec.Volumes.Should().HaveCount(2);
        Toolchain(pod).Env.Select(e => e.Name).Should().Equal("JOB_ID", "REDIS_URL");
    }

    [Fact]
    public void PodSpecBuilder_Hardened_MountsWritableRootHomeAndSetsHome()
    {
        var pod = Build(SandboxSecurityContext.Hardened);

        var mount = Toolchain(pod).VolumeMounts.Single(m => m.MountPath == "/root");
        mount.ReadOnlyProperty.Should().NotBe(true);
        pod.Spec.Volumes.Single(v => v.Name == mount.Name).EmptyDir.Should().NotBeNull();
        Toolchain(pod).Env.Should().Contain(e => e.Name == "HOME" && e.Value == "/root");
    }

    [Fact]
    public void PodSpecBuilder_Hardened_DeclaresWorkASafeGitDirectory()
    {
        var env = Toolchain(Build(SandboxSecurityContext.Hardened)).Env.ToDictionary(e => e.Name, e => e.Value);

        env["GIT_CONFIG_COUNT"].Should().Be("1");
        env["GIT_CONFIG_KEY_0"].Should().Be("safe.directory");
        env["GIT_CONFIG_VALUE_0"].Should().Be("*");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PodSpecBuilder_AnyProfile_DoesNotMountTheServiceAccountToken(bool root)
    {
        Build(new SandboxSecurityContext(RunAsRoot: root)).Spec.AutomountServiceAccountToken.Should().BeFalse();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PodSpecBuilder_AnyProfile_DisallowsPrivilegeEscalation(bool root)
    {
        var pod = Build(new SandboxSecurityContext(RunAsRoot: root));

        pod.Spec.SecurityContext.SeccompProfile.Type.Should().Be("RuntimeDefault");
        pod.Spec.InitContainers.Concat(pod.Spec.Containers)
            .Should().OnlyContain(c => c.SecurityContext.AllowPrivilegeEscalation == false);
    }

    [Fact]
    public void PodSpecBuilder_RunAsRoot_KeepsDefaultCapabilitiesAndNoUser()
    {
        var pod = Build(SandboxSecurityContext.Root);

        pod.Spec.SecurityContext.RunAsUser.Should().BeNull();
        pod.Spec.SecurityContext.RunAsNonRoot.Should().BeNull();
        Toolchain(pod).SecurityContext.Capabilities.Should().BeNull();
        Toolchain(pod).VolumeMounts.Should().NotContain(m => m.MountPath == "/root");
        Toolchain(pod).Env.Should().NotContain(e => e.Name == "HOME");
    }

    [Fact]
    public void PodSpecBuilder_InitContainer_HardenedLikeTheToolchain()
    {
        var pod = Build(SandboxSecurityContext.Hardened);

        var loader = pod.Spec.InitContainers.Single();
        loader.SecurityContext.Capabilities.Drop.Should().Equal("ALL");
        loader.SecurityContext.AllowPrivilegeEscalation.Should().BeFalse();
    }
}
