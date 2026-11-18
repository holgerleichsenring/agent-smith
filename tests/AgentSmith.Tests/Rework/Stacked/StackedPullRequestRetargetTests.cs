using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.TestSupport.TicketRequirementEdges;

namespace AgentSmith.Tests.Rework.Stacked;

/// <summary>
/// 2026-11-17-9a39c: a stacked dependent's pull request on a landed prerequisite's branch is moved, by a 'retarget' run,
/// to where the prerequisite's own pull request landed; anything else launches no run.
/// </summary>
public sealed class StackedPullRequestRetargetTests
{
    private const string DependentPr = "https://git.test/o/api/pull/8";
    private const string PrerequisitePr = "https://git.test/o/api/pull/7";
    private readonly InMemoryTicketFacts _facts = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IActiveRunLease> _leases = new();
    private readonly Mock<ISourceProvider> _provider = new();
    private AgentSmith.Domain.Models.TicketId? _shownOn;
    private readonly Mock<IActRun> _runs = new();
    private readonly List<PullRequestMove> _moved = [];

    public StackedPullRequestRetargetTests()
    {
        _facts.Edges.Add(Edge("42", "41"));
        _facts.Land("41");
        _attempts.Setup(a => a.LatestAsync(Project, "41", null, default)).ReturnsAsync(Attempt(PrerequisitePr));
        _provider.Setup(p => p.ReadPullRequestBaseAsync(PrerequisitePr, default)).ReturnsAsync("main");
        _runs.Setup(r => r.RunAsync(Project, PipelinePresets.RetargetName, It.IsAny<IReadOnlyDictionary<string, object>>(),
                It.IsAny<AgentSmith.Domain.Models.TicketId?>()))
            .Callback<string, string, IReadOnlyDictionary<string, object>, AgentSmith.Domain.Models.TicketId?>((_, _, seeds, shown) =>
            {
                _shownOn = shown;
                _moved.AddRange(((RetargetPullRequestsRequest)seeds[ContextKeys.RetargetPullRequestsRequest]).Moves);
                ((ActSlot<int>)seeds[ContextKeys.ActOutcome]).Set(_moved.Count);
            })
            .ReturnsAsync(CommandResult.Ok("moved"));
    }

    [Fact]
    public async Task RetargetAsync_APullRequestOnTheLandedBranch_MovesToWhereThePrerequisiteLanded()
    {
        _provider.Setup(p => p.ReadPullRequestBaseAsync(DependentPr, default)).ReturnsAsync("agent-smith/41");

        (await Retarget().RetargetAsync(Of(), "42", Attempt(DependentPr), default)).Should().Be(1);

        _moved.Should().Equal(new PullRequestMove("api", DependentPr, "agent-smith/41", "main"));
        _shownOn!.Value.Should().Be("42", "2026-11-17-b34ed: the run is listed on the dependent's page");
    }

    [Fact]
    public async Task RetargetAsync_APullRequestOnAnotherBranch_LaunchesNoRun()
    {
        _provider.Setup(p => p.ReadPullRequestBaseAsync(DependentPr, default)).ReturnsAsync("main");

        (await Retarget().RetargetAsync(Of(), "42", Attempt(DependentPr), default)).Should().Be(0);

        _runs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RetargetAsync_ALiveRun_IsLeftForItsEnd()
    {
        _provider.Setup(p => p.ReadPullRequestBaseAsync(DependentPr, default)).ReturnsAsync("agent-smith/41");
        _leases.Setup(l => l.GetByTicketAsync(Project, new TicketId("42"), default)).ReturnsAsync(new StaleLease(Project, new TicketId("42"), "run-42", null));

        (await Retarget().RetargetAsync(Of(), "42", Attempt(DependentPr), default)).Should().Be(0);
    }

    [Fact]
    public async Task RetargetAsync_NoLandedRequirement_ReadsNoPullRequest()
    {
        _facts.Landed.Clear();

        (await Retarget().RetargetAsync(Of(), "42", Attempt(DependentPr), default)).Should().Be(0);

        _provider.Verify(p => p.ReadPullRequestBaseAsync(It.IsAny<string>(), default), Times.Never);
    }

    private StackedPullRequestRetarget Retarget()
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        var scopes = new ServiceCollection().AddScoped(_ => _runs.Object).AddScoped<PullRequestRetargetRun>()
            .BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new(_facts, _facts, new UnmetTicketRequirements(_facts, _facts, new InMemorySpecApprovalStore(),
                new GreenPrerequisites(_facts, _attempts.Object, _facts)),
            _attempts.Object, _leases.Object, sources.Object, scopes, NullLogger<StackedPullRequestRetarget>.Instance);
    }

    private static PreviousAttempt Attempt(string url) =>
        new("run", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true)
        {
            OpenedPullRequest = true, PullRequestUrls = new Dictionary<string, string> { ["api"] = url },
        };

    private static ResolvedProject Of() => new()
    {
        Name = Project, Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.Jira },
        Repos = [new RepoConnection { Name = "api" }],
    };
}
