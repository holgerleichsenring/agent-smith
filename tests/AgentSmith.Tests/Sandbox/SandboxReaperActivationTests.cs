using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using StackExchange.Redis;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// p0465: the Docker backend is auto-detected from /var/run/docker.sock, so every dev
/// machine and every side-instance used to arm a daemon-wide reaper. The reaper's
/// whole judgement rests on the liveness store answering "is this run alive?", so it
/// runs where a durable lease can answer and stands down — loudly — where it cannot.
/// </summary>
[Collection(EnvVarCollection.Name)]
public sealed class SandboxReaperActivationTests : IDisposable
{
    private readonly string? _originalSandboxType;
    private readonly string? _originalOverride;

    public SandboxReaperActivationTests()
    {
        _originalSandboxType = Environment.GetEnvironmentVariable("SANDBOX_TYPE");
        _originalOverride = Environment.GetEnvironmentVariable(SandboxReaperActivation.OverrideEnvVar);
        Environment.SetEnvironmentVariable("SANDBOX_TYPE", "docker");
        Environment.SetEnvironmentVariable(SandboxReaperActivation.OverrideEnvVar, null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SANDBOX_TYPE", _originalSandboxType);
        Environment.SetEnvironmentVariable(SandboxReaperActivation.OverrideEnvVar, _originalOverride);
    }

    [Fact]
    public void Decide_NoDurableLease_StandsDown()
    {
        var activation = SandboxReaperActivation.Decide(leaseAnswersLiveness: false, operatorOverride: null);

        activation.ShouldRun.Should().BeFalse();
        activation.Reason.Should().Contain("lease");
    }

    [Fact]
    public void Decide_DurableLease_Runs() =>
        SandboxReaperActivation.Decide(leaseAnswersLiveness: true, operatorOverride: null)
            .ShouldRun.Should().BeTrue();

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Decide_OperatorOverride_WinsEitherWay(string setting, bool expected)
    {
        SandboxReaperActivation.Decide(leaseAnswersLiveness: !expected, operatorOverride: setting)
            .ShouldRun.Should().Be(expected);
    }

    [Fact]
    public void AddSandbox_WithoutADurableLease_RegistersNoReaper_ButSaysSo()
    {
        var services = BuildServices(new NoOpActiveRunLease());

        HostedServices(services).Should().NotContain(typeof(SandboxOrphanReaper));
        HostedServices(services).Should().Contain(typeof(SandboxReaperStandDownNotice));
    }

    [Fact]
    public void AddSandbox_WithADurableLease_RegistersTheReaper()
    {
        var services = BuildServices(Mock.Of<IActiveRunLease>());

        HostedServices(services).Should().Contain(typeof(SandboxOrphanReaper));
        HostedServices(services).Should().NotContain(typeof(SandboxReaperStandDownNotice));
    }

    [Fact]
    public void AddSandbox_ExplicitlyDisabled_RegistersNoReaper()
    {
        Environment.SetEnvironmentVariable(SandboxReaperActivation.OverrideEnvVar, "false");

        var services = BuildServices(Mock.Of<IActiveRunLease>());

        HostedServices(services).Should().NotContain(typeof(SandboxOrphanReaper));
        services.BuildServiceProvider().GetRequiredService<SandboxReaperActivation>()
            .ShouldRun.Should().BeFalse();
    }

    private static IServiceCollection BuildServices(IActiveRunLease lease)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(Mock.Of<IConnectionMultiplexer>());
        services.AddSingleton(lease);
        services.AddSandbox();
        return services;
    }

    private static IEnumerable<Type> HostedServices(IServiceCollection services) =>
        services
            .Where(d => d.ServiceType == typeof(IHostedService))
            .Select(d => d.ImplementationType ?? typeof(object));
}
