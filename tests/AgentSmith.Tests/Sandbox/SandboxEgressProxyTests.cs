using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-10-25-c5a7g: an operator-set egress proxy reaches every sandbox toolchain's env, in both backends.</summary>
public sealed class SandboxEgressProxyTests
{
    private const string Proxy = "http://agentsmith-egress-proxy.agentsmith.svc.cluster.local:3128";

    private static Dictionary<string, string> PodEnv(SandboxEgressProxy? proxy)
    {
        var builder = new PodSpecBuilder(new SandboxPodLabels(new SandboxOwnerIdentity("store-test")),
            new PodSecurity(new KubernetesSandboxOptions { EgressProxy = proxy }));
        var pod = builder.Build("p", "j", "redis:6379", new SandboxSpec("node:20", ResourceLimits.Default, "agent:1"), null);
        return pod.Spec.Containers.Single(c => c.Name == "toolchain").Env.ToDictionary(e => e.Name, e => e.Value);
    }

    [Fact]
    public void PodSpecBuilder_EgressProxySet_AddsProxyVariablesAndNoProxyForRedis()
    {
        var env = PodEnv(new SandboxEgressProxy(Proxy));

        foreach (var name in new[] { "HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy" })
            env[name].Should().Be(Proxy);
        env["NO_PROXY"].Should().Be(SandboxEgressProxy.NoProxy);
        env["no_proxy"].Should().Be(SandboxEgressProxy.NoProxy);
        env["REDIS_URL"].Should().Be("redis:6379");
    }

    [Fact]
    public void PodSpecBuilder_EgressProxyUnset_AddsNothing()
    {
        PodEnv(proxy: null).Keys.Should().NotContain(k => k.Contains("proxy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DockerContainerSpecBuilder_EgressProxySet_AddsProxyVariables()
    {
        var builder = new DockerContainerSpecBuilder(new SandboxOwnerIdentity("store-test"),
            new DockerHardening(new DockerSandboxOptions { EgressProxy = new SandboxEgressProxy("http://egress-proxy:3128") }));

        var spec = builder.BuildToolchain("tc", new DockerSandboxVolumeSet("s", "w", null), "job", "redis:6379",
            new SandboxSpec("img", ResourceLimits.Default, "ai"));

        spec.Env.Should().Contain("HTTPS_PROXY=http://egress-proxy:3128")
            .And.Contain("http_proxy=http://egress-proxy:3128")
            .And.Contain($"NO_PROXY={SandboxEgressProxy.NoProxy}");
    }

    [Fact]
    public void SandboxEgressProxy_NoProxy_CoversLoopbackAndClusterSuffixes()
    {
        SandboxEgressProxy.NoProxy.Split(',').Should().BeEquivalentTo("localhost", "127.0.0.1", ".svc", ".cluster.local");
    }

    [Fact]
    public void SandboxEgressProxy_ReadFromTheServerEnvironment_InBothBackends()
    {
        var env = new Dictionary<string, string> { ["SANDBOX_EGRESS_PROXY"] = " http://egress-proxy:3128 " };

        new KubernetesSandboxOptionsReader(n => env.GetValueOrDefault(n)).Read().EgressProxy!.Url
            .Should().Be("http://egress-proxy:3128");
        new DockerSandboxOptionsReader(n => env.GetValueOrDefault(n)).Read().EgressProxy!.Url
            .Should().Be("http://egress-proxy:3128");
        new DockerSandboxOptionsReader(_ => null).Read().EgressProxy.Should().BeNull();
    }
}
