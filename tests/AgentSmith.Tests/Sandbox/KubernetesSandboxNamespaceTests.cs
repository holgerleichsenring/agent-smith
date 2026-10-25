using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using k8s;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-10-25-c5a7c: sandbox pods may live in a namespace of their own.</summary>
public sealed class KubernetesSandboxNamespaceTests
{
    private static KubernetesSandboxOptions Read(Dictionary<string, string> env) =>
        new KubernetesSandboxOptionsReader(name => env.GetValueOrDefault(name)).Read();

    [Fact]
    public void KubernetesSandboxRegistrations_SandboxNamespaceSet_PodsGoThere()
    {
        var options = Read(new()
        {
            ["K8S_NAMESPACE"] = "agentsmith", ["K8S_SANDBOX_NAMESPACE"] = "agentsmith-sandboxes",
            ["SANDBOX_REDIS_URL"] = "redis.agentsmith.svc.cluster.local:6379",
        });

        options.Namespace.Should().Be("agentsmith-sandboxes");
        options.ServerNamespace.Should().Be("agentsmith");
        options.RedisUrl.Should().Be("redis.agentsmith.svc.cluster.local:6379");
        options.Misconfiguration.Should().BeNull();
    }

    [Fact]
    public void KubernetesSandboxRegistrations_Unset_FallsBackToServerNamespaceAndRedisUrl()
    {
        var options = Read(new() { ["K8S_NAMESPACE"] = "agentsmith", ["REDIS_URL"] = "redis:6379" });

        options.Namespace.Should().Be("agentsmith");
        options.RedisUrl.Should().Be("redis:6379");
        options.Misconfiguration.Should().BeNull();
    }

    [Fact]
    public void KubernetesSandboxRegistrations_SandboxRedisUrl_OwnerIdentityStillFromRedisUrl()
    {
        var options = Read(new() { ["REDIS_URL"] = "redis:6379", ["SANDBOX_REDIS_URL"] = "redis.ns.svc:6379" });

        options.OwnerRedisUrl.Should().Be("redis:6379");
        options.RedisUrl.Should().Be("redis.ns.svc:6379");
    }

    [Fact]
    public async Task SandboxNamespaceProbe_OtherNamespaceWithoutSandboxRedisUrl_IsBlocking()
    {
        var options = Read(new() { ["K8S_NAMESPACE"] = "agentsmith", ["K8S_SANDBOX_NAMESPACE"] = "agentsmith-sandboxes" });

        var findings = await new SandboxNamespaceProbe(options).ProbeAsync(CancellationToken.None);

        findings.Should().ContainSingle().Which.Severity.Should().Be(StartupFindingSeverity.Blocking);
        findings[0].Reason.Should().Contain("SANDBOX_REDIS_URL");
    }

    [Fact]
    public async Task KubernetesSandboxFactory_OtherNamespaceWithoutSandboxRedisUrl_RefusesToSpawn()
    {
        var options = Read(new() { ["K8S_NAMESPACE"] = "agentsmith", ["K8S_SANDBOX_NAMESPACE"] = "agentsmith-sandboxes" });
        var client = new Mock<IKubernetes>(MockBehavior.Strict);
        var factory = new KubernetesSandboxFactory(client.Object, Mock.Of<IConnectionMultiplexer>(),
            new PodSpecBuilder(new SandboxPodLabels(new SandboxOwnerIdentity("store-test")), new PodSecurity(options)),
            options, Options.Create(new SandboxGlobalConfig()), Mock.Of<IWireProtocolWatcher>(), NullLoggerFactory.Instance);

        var act = () => factory.CreateAsync(new SandboxSpec("node:20", ResourceLimits.Default), CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Message.Should().Contain("SANDBOX_REDIS_URL");
    }
}
