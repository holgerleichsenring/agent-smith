using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Sandbox;
using AgentSmith.Tests.TestHelpers;
using Docker.DotNet;
using Docker.DotNet.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-18-0f27: where the Docker concurrent-sandbox bound comes from. The stored
/// sandbox settings first, then the SANDBOX_MAX_CONCURRENT environment variable — which
/// stays reachable only because an unconfigured bound is NULL — then the built-in
/// default. The probe resolves through the configuration loader at the moment it
/// decides, so a catalog change reaches the next decision without a restart.
/// <para>
/// Joins the env-var collection: two cases mutate a process-global variable, and added
/// blind they would pass alone and flake under parallelism.
/// </para>
/// </summary>
[Collection(TestSupport.EnvVarCollection.Name)]
public sealed class ConcurrentSandboxBoundTests
{
    private const string EnvVar = DockerCapacityProbe.BoundEnvironmentVariable;
    private static readonly SandboxOwnerIdentity Owner = new("store-0123456789abcdef");
    private static readonly DockerSandboxQuery Query = new(Owner);

    private static RunFootprint OneSandbox() =>
        new(Orchestrator: null, [new ResourceLimits("500m", "1000m", "1Gi", "2Gi")]);

    [Fact]
    public async Task Bound_SetInTheCatalog_IsWhatTheProbeUses()
    {
        var docker = DockerWithRunningSandboxes(count: 2);
        var probe = Probe(docker, Loader(Catalog(3)));

        // 2 running + 1 needed fits under 3 — the built-in default of 2 would deny.
        (await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None))
            .Admitted.Should().BeTrue();
    }

    [Fact]
    public async Task Bound_ChangedInTheCatalog_AppliesToTheNextDecisionWithoutARestart()
    {
        var docker = DockerWithRunningSandboxes(count: 2);
        var bound = 3;
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(() => Catalog(bound));
        var probe = Probe(docker, loader.Object);

        (await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None))
            .Admitted.Should().BeTrue();

        // The same probe instance — no restart, no re-composition, just a saved setting.
        bound = 2;

        (await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None))
            .Admitted.Should().BeFalse("the probe re-reads the catalog at the moment it decides");
    }

    [Fact]
    public async Task Bound_NullInTheCatalog_FallsBackToTheEnvironmentVariable()
    {
        using var env = new EnvVarScope(EnvVar, "3");
        var docker = DockerWithRunningSandboxes(count: 2);
        var probe = Probe(docker, Loader(Catalog(null)));

        (await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None))
            .Admitted.Should().BeTrue("nobody configured a bound, so the variable is still the fallback");
    }

    [Fact]
    public async Task Bound_NullAndNoEnvironmentVariable_IsTheBuiltInDefault()
    {
        using var env = new EnvVarScope(EnvVar, null);
        var docker = DockerWithRunningSandboxes(count: 2);
        var probe = Probe(docker, Loader(Catalog(null)));

        // The built-in default is 2, and 2 running + 1 needed does not fit.
        (await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None))
            .Admitted.Should().BeFalse();
        DockerCapacityProbe.DefaultBound.Should().Be(2);
    }

    [Fact]
    public async Task Bound_ZeroInTheCatalog_StillMeansUnbounded()
    {
        // Five sandboxes already running: under ANY positive bound this would be denied,
        // and an unbounded probe does not even ask the daemon.
        var docker = DockerWithRunningSandboxes(count: 5);
        var probe = Probe(docker, Loader(Catalog(0)));

        (await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None))
            .Admitted.Should().BeTrue();
        Mock.Get(docker.Object.Containers).Verify(
            c => c.ListContainersAsync(
                It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Bound_ResolutionChanged_IsReportedOnceWithItsSource()
    {
        var docker = DockerWithRunningSandboxes(count: 0);
        var bound = 3;
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(() => Catalog(bound));
        var logger = new CapturingLogger<DockerCapacityProbe>();
        var probe = Probe(docker, loader.Object, logger);

        // SEQUENTIAL: the change-detection state is an instance field, and three call
        // sites can probe at once — a concurrent first decision would report twice.
        await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None);
        bound = 5;
        await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None);

        logger.Lines.Should().HaveCount(2);
        logger.Lines[0].Should().Contain("3").And.Contain("the sandbox settings");
        logger.Lines[1].Should().Contain("5").And.Contain("the sandbox settings");
    }

    [Fact]
    public async Task Bound_ResolutionUnchanged_IsNotReportedAgain()
    {
        var docker = DockerWithRunningSandboxes(count: 0);
        var logger = new CapturingLogger<DockerCapacityProbe>();
        var probe = Probe(docker, Loader(Catalog(3)), logger);

        await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None);
        await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None);
        await probe.HasCapacityAsync(OneSandbox(), CancellationToken.None);

        logger.Lines.Should().ContainSingle("a steady bound is answerable from one line, not one per decision");
    }

    // ---- helpers ----

    private static AgentSmithConfig Catalog(int? bound) =>
        new() { Sandbox = new SandboxGlobalConfig { MaxConcurrentSandboxes = bound } };

    private static IConfigurationLoader Loader(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return loader.Object;
    }

    private static DockerCapacityProbe Probe(
        Mock<IDockerClient> docker, IConfigurationLoader loader,
        ILogger<DockerCapacityProbe>? logger = null) =>
        new(docker.Object, Query, loader, new ServerContext("agentsmith.yml"),
            logger ?? NullLogger<DockerCapacityProbe>.Instance);

    private static Mock<IDockerClient> DockerWithRunningSandboxes(int count)
    {
        var containers = Enumerable.Range(0, count)
            .Select(i => new ContainerListResponse
            {
                ID = $"c{i}",
                Labels = new Dictionary<string, string>
                {
                    [DockerContainerSpecBuilder.JobIdLabel] = $"job{i}",
                    [DockerContainerSpecBuilder.OwnerLabel] = Owner.Value,
                },
            })
            .ToList();

        var ops = new Mock<IContainerOperations>();
        ops.Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IList<ContainerListResponse>)containers);

        var docker = new Mock<IDockerClient>();
        docker.SetupGet(d => d.Containers).Returns(ops.Object);
        return docker;
    }

    /// <summary>Sets one environment variable for the life of the scope and restores it.</summary>
    private sealed class EnvVarScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvVarScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
