using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Spawning;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.ChatLaunch;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.Spawning;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>
/// 2026-11-16-f3fbc: each ReworkLaunch entry marks its start kind on the claim — an act carries neither marker, a
/// series start the series marker, a precondition release the release marker — and every one is a named start the
/// boundary gate admits.
/// </summary>
public sealed class ReworkLaunchStartKindTests : IDisposable
{
    private static readonly ResolvedProject Alpha = new()
    {
        Name = "alpha", Repos = [new RepoConnection { Name = "api" }],
        Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
        GithubTrigger = new WebhookTriggerConfig { TriggerStatuses = ["Todo"] },
    };

    private readonly ServiceProvider _services;
    private ClaimRequest? _claimed;

    public ReworkLaunchStartKindTests() => _services = new ServiceCollection().AddScoped(_ => Launcher()).BuildServiceProvider();

    public void Dispose() => _services.Dispose();

    private ReworkLaunch Launch() => new(_services.GetRequiredService<IServiceScopeFactory>());

    private ChatTicketRunLauncher Launcher()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>()))
            .Returns(new AgentSmithConfig { Projects = new Dictionary<string, ResolvedProject> { ["alpha"] = Alpha } });
        var claims = new Mock<ITicketClaimService>();
        claims.Setup(c => c.ClaimAsync(It.IsAny<ClaimRequest>(), It.IsAny<AgentSmithConfig>(), It.IsAny<CancellationToken>()))
            .Callback<ClaimRequest, AgentSmithConfig, CancellationToken>((r, _, _) => _claimed = r)
            .ReturnsAsync(ClaimResult.Claimed());
        var funnel = new SpawnPipelineRunsUseCase(
            claims.Object, CapacityTestDoubles.StubCalculator(), CapacityTestDoubles.AlwaysReserve(),
            CapacityTestDoubles.EmptyQueue(), CapacityTestDoubles.NoCorpses(), CapacityTestDoubles.NoHolds(),
            CapacityTestDoubles.AlwaysAdmit(), ApprovedSetDoubles.Carrier(), CapacityTestDoubles.NoNudge(),
            CapacityTestDoubles.NoStandingRefusal(), Mock.Of<IActiveRunLease>(), NullLogger<SpawnPipelineRunsUseCase>.Instance,
            new AgentSmith.Application.Services.Resume.NullPreviousAttemptReader(), TicketRequirementDoubles.None());
        return new ChatTicketRunLauncher(loader.Object, new ServerContext("c.yml"), funnel, NullLogger<ChatTicketRunLauncher>.Instance);
    }

    private bool Marked(string key) => _claimed!.InitialContext!.ContainsKey(key);

    [Fact]
    public async Task ReworkLaunch_PreconditionRelease_MarksTheRelease()
    {
        (await Launch().StartPreconditionReleaseAsync("alpha", "42", CancellationToken.None)).Kind.Should().Be(ReworkOutcomeKind.Started);

        Marked(ContextKeys.PreconditionRelease).Should().BeTrue();
        Marked(ContextKeys.SeriesAutoStart).Should().BeFalse();
        BoundaryStopGate.AdmitsStart(_claimed!.InitialContext).Should().BeTrue("a release passes the hold its predecessor set");
    }

    [Fact]
    public async Task ReworkLaunch_Series_MarksTheSeriesOnly()
    {
        await Launch().StartSeriesAttemptAsync("alpha", "42", CancellationToken.None);

        Marked(ContextKeys.SeriesAutoStart).Should().BeTrue();
        Marked(ContextKeys.PreconditionRelease).Should().BeFalse();
    }

    [Fact]
    public async Task ReworkLaunch_Act_CarriesNeitherMarker()
    {
        await Launch().LaunchAsync("alpha", "42", PipelinePresets.CodeName, CancellationToken.None);

        Marked(ContextKeys.SeriesAutoStart).Should().BeFalse();
        Marked(ContextKeys.PreconditionRelease).Should().BeFalse();
        _claimed!.InitialContext.IsRequestedByName().Should().BeTrue();
    }
}
