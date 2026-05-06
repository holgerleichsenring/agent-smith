using AgentSmith.Server.Extensions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using AgentSmith.Tests.TestSupport;
using StackExchange.Redis;
using Moq;

namespace AgentSmith.Tests.Sandbox;

[Collection(EnvVarCollection.Name)]
public sealed class SandboxBackendDetectionTests : IDisposable
{
    private readonly string? _originalSandboxType;
    private readonly string? _originalK8sHost;

    public SandboxBackendDetectionTests()
    {
        _originalSandboxType = Environment.GetEnvironmentVariable("SANDBOX_TYPE");
        _originalK8sHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
        Environment.SetEnvironmentVariable("SANDBOX_TYPE", null);
        Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SANDBOX_TYPE", _originalSandboxType);
        Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", _originalK8sHost);
    }

    [Fact]
    public void AddSandbox_WhenSandboxTypeIsDocker_RegistersDockerBackend()
    {
        Environment.SetEnvironmentVariable("SANDBOX_TYPE", "docker");
        var sp = BuildProvider();

        var info = sp.GetRequiredService<SandboxBackendInfo>();

        info.Backend.Should().Be(SandboxBackend.Docker);
    }

    [Fact]
    public void AddSandbox_WhenSandboxTypeIsKubernetes_RegistersKubernetesBackend()
    {
        Environment.SetEnvironmentVariable("SANDBOX_TYPE", "kubernetes");
        var sp = BuildProvider(skipKubeClient: true);

        var info = sp.GetRequiredService<SandboxBackendInfo>();

        info.Backend.Should().Be(SandboxBackend.Kubernetes);
    }

    [Fact]
    public void AddSandbox_WhenSandboxTypeIsInProcess_RegistersInProcessBackend()
    {
        Environment.SetEnvironmentVariable("SANDBOX_TYPE", "inprocess");
        var sp = BuildProvider();

        var info = sp.GetRequiredService<SandboxBackendInfo>();

        info.Backend.Should().Be(SandboxBackend.InProcess);
    }

    [Fact]
    public void AddSandbox_WhenK8sServiceHostSet_PrefersKubernetesOverInprocessFallback()
    {
        Environment.SetEnvironmentVariable("KUBERNETES_SERVICE_HOST", "10.0.0.1");
        var sp = BuildProvider(skipKubeClient: true);

        var info = sp.GetRequiredService<SandboxBackendInfo>();

        info.Backend.Should().Be(SandboxBackend.Kubernetes);
    }

    private static ServiceProvider BuildProvider(bool skipKubeClient = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IConnectionMultiplexer>());
        services.AddSandbox();
        if (skipKubeClient)
        {
            var descriptors = services.Where(d => d.ServiceType.FullName?.Contains("k8s.IKubernetes") ?? false).ToList();
            foreach (var d in descriptors) services.Remove(d);
            services.AddSingleton(Mock.Of<k8s.IKubernetes>());
        }
        return services.BuildServiceProvider();
    }
}
