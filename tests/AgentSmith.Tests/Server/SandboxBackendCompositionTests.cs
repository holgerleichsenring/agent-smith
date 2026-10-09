using AgentSmith.Application.Services.Preflight;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Server.Services.Startup;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Server;

/// <summary>
/// Every run executes in the server and creates its sandboxes through the composed backend,
/// so nothing spawns a job, and the preflight and startup probes ask the backend that runs.
/// </summary>
[Trait("TestProcess", "env-1")]
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class SandboxBackendCompositionTests
{
    [Fact]
    public void ServerComposition_RegistersNoIJobSpawner()
    {
        var services = Composed("inprocess");

        services.Should().NotContain(d =>
            d.ServiceType.Name.Contains("JobSpawner")
            || (d.ImplementationType != null && d.ImplementationType.Name.Contains("JobSpawner")));
        typeof(ServerCompositionBuilder).Assembly.GetTypes()
            .Should().NotContain(t => t.Name.Contains("JobSpawner"));
    }

    [Theory]
    [InlineData("inprocess", typeof(SandboxRoundTripProbe))]
    [InlineData("docker", typeof(DockerSandboxBackendProbe))]
    [InlineData("kubernetes", typeof(KubernetesSandboxBackendProbe))]
    public void PreflightSandboxProbe_ProbesComposedSandboxBackend(string backend, Type probe)
    {
        var registered = Composed(backend).Last(d => d.ServiceType == typeof(IPreflightSandboxProbe));

        registered.ImplementationType.Should().Be(probe);
    }

    [Fact]
    public async Task SandboxBackendProbe_Unreachable_IsBlockingAndNamesTheBackend()
    {
        var backend = new Mock<IPreflightSandboxProbe>();
        backend.SetupGet(b => b.BackendLabel).Returns("Kubernetes");
        backend.Setup(b => b.ProbeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectionProbeResult.Unreachable(3, "forbidden"));

        var finding = (await new SandboxBackendProbe(backend.Object).ProbeAsync(default))
            .Should().ContainSingle().Which;

        finding.Subsystem.Should().Be(StartupSubsystems.SandboxBackend);
        finding.Severity.Should().Be(StartupFindingSeverity.Blocking);
        finding.Reason.Should().Contain("Kubernetes").And.Contain("forbidden");
    }

    [Fact]
    public async Task SandboxBackendProbe_Reachable_FindsNothing()
    {
        var backend = new Mock<IPreflightSandboxProbe>();
        backend.Setup(b => b.ProbeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConnectionProbeResult.Reachable(1));

        (await new SandboxBackendProbe(backend.Object).ProbeAsync(default)).Should().BeEmpty();
    }

    private static IServiceCollection Composed(string sandboxType)
    {
        var previous = Environment.GetEnvironmentVariable("SANDBOX_TYPE");
        Environment.SetEnvironmentVariable("SANDBOX_TYPE", sandboxType);
        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddProvider(NullLoggerProvider.Instance));
            ServerCompositionBuilder.ConfigureServices(services, "agentsmith.yml");
            return services;
        }
        finally
        {
            Environment.SetEnvironmentVariable("SANDBOX_TYPE", previous);
        }
    }
}
