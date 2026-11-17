using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Preconditions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Preconditions;

/// <summary>2026-11-16-f3fbc: the passes of one config epoch — the first runs at once, so a settings save newer than
/// a config stop releases it without waiting for the interval.</summary>
public sealed class PreconditionReleaseHostedServiceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Passes_FirstPass_RunsAtOnce()
    {
        using var cts = new CancellationTokenSource();
        var nudged = new List<ReworkNudgeRequest>();
        var stuck = new Mock<IStuckOnPreconditionReader>();
        stuck.Setup(s => s.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StuckOnPrecondition("p", "5", "run-5", RunEndKinds.Preflight, new EndDetail("agents.a.model", []), T0)]);
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReworkNudgeRequest, CancellationToken>((r, _) => { nudged.Add(r); cts.Cancel(); }).Returns(Task.CompletedTask);
        using var services = new ServiceCollection().AddSingleton(new PreconditionReleasePass(stuck.Object,
            new PreconditionHoldCheck(new ContextImageBoundary(Mock.Of<IImageRegistryTrust>())),
            new RepoReadiness(null!, Mock.Of<ISourceProviderFactory>(), NullLogger<RepoReadiness>.Instance),
            Mock.Of<IConfigChangeClock>(c => c.LastChangedAtAsync(It.IsAny<CancellationToken>()) == Task.FromResult<DateTimeOffset?>(T0.AddMinutes(1))),
            nudges.Object, NullLogger<PreconditionReleasePass>.Instance)).BuildServiceProvider();
        var config = new AgentSmithConfig { Projects = new Dictionary<string, ResolvedProject> { ["p"] = new() { Name = "p" } } };
        var service = new PreconditionReleaseHostedService(services, new ServerContext("c.yml"), Mock.Of<IConfigurationLoader>(),
            NullLogger<PreconditionReleaseHostedService>.Instance);

        var passes = () => service.PassesAsync(config, cts.Token);

        await passes.Should().ThrowAsync<OperationCanceledException>("the epoch ended while waiting for the interval");
        nudged.Should().ContainSingle().Which.Origin.Should().Be(ReworkNudgeOrigin.PreconditionsMet);
    }
}
