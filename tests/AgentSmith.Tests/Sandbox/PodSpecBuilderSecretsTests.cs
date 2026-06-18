using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0272: operator-declared sandbox.secrets reach the k8s pod as secretKeyRef env
/// vars and read-only Secret-volume file mounts — never as plaintext in the pod
/// spec. A spec with no secrets leaves the baseline pod untouched.
/// </summary>
public sealed class PodSpecBuilderSecretsTests
{
    private static readonly PodSpecBuilder Builder = new();

    private static SandboxSpec SpecWith(ResolvedSandboxSecrets? secrets) => new(
        ToolchainImage: "node:20-bookworm",
        Resources: ResourceLimits.Default,
        AgentImage: "agent:1",
        Secrets: secrets);

    [Fact]
    public void Build_SecretEnv_InjectsSecretKeyRefEnvVar()
    {
        var secrets = new ResolvedSandboxSecrets(
            [new SecretEnvBinding("SF_CLIENT_ID", new SecretRef("sf-creds", "client-id"))], []);

        var pod = Builder.Build("p", "j", "redis:6379", SpecWith(secrets), owner: null);

        var toolchain = pod.Spec.Containers.Single(c => c.Name == "toolchain");
        var env = toolchain.Env.Single(e => e.Name == "SF_CLIENT_ID");
        env.Value.Should().BeNull();
        env.ValueFrom.SecretKeyRef.Name.Should().Be("sf-creds");
        env.ValueFrom.SecretKeyRef.Key.Should().Be("client-id");
    }

    [Fact]
    public void Build_SecretFile_MountsSecretVolumeReadOnlyAtPath()
    {
        var secrets = new ResolvedSandboxSecrets(
            [], [new SecretFileMount("/secrets/server.key", new SecretRef("sf-creds", "jwt-key"))]);

        var pod = Builder.Build("p", "j", "redis:6379", SpecWith(secrets), owner: null);

        var toolchain = pod.Spec.Containers.Single(c => c.Name == "toolchain");
        var mount = toolchain.VolumeMounts.Single(m => m.MountPath == "/secrets/server.key");
        mount.ReadOnlyProperty.Should().BeTrue();
        mount.SubPath.Should().Be("server.key");

        var volume = pod.Spec.Volumes.Single(v => v.Name == mount.Name);
        volume.Secret.SecretName.Should().Be("sf-creds");
        volume.Secret.Items.Single().Key.Should().Be("jwt-key");
        volume.Secret.Items.Single().Path.Should().Be("server.key");
    }

    [Fact]
    public void Build_NoSecrets_PodSpecUnchangedFromBaseline()
    {
        var pod = Builder.Build("p", "j", "redis:6379", SpecWith(secrets: null), owner: null);

        var toolchain = pod.Spec.Containers.Single(c => c.Name == "toolchain");
        toolchain.VolumeMounts.Should().HaveCount(2);
        pod.Spec.Volumes.Should().HaveCount(2);
        toolchain.Env.Select(e => e.Name).Should().BeEquivalentTo("JOB_ID", "REDIS_URL");
    }
}
