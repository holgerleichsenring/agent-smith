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
    private readonly Mock<IPreviousAttemptReader> _attempts = new() { CallBase = true }; // 9719d: the set reads loop LatestAsync
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
                It.IsAny<AgentSmith.Domain.Models.TicketId?>(), It.IsAny<string?>()))
            .Callback<string, string, IReadOnlyDictionary<string, object>, AgentSmith.Domain.Models.TicketId?, string?>((_, _, seeds, shown, _) =>
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

    // 2026-11-23-97c2b: chain S1 <- S2(api, worker) <- S3(api); the dependent 42 requires all three and sat on S1's branch.
    [Fact]
    public async Task RetargetAsync_S1Lands_EachRepoMovesToItsOwnNextTip()
    {
        StackOnS1();

        (await Retarget().RetargetAsync(Of("worker"), "42", Attempt(DependentPr, (Worker, DependentWorkerPr)), default)).Should().Be(2);

        _moved.Should().BeEquivalentTo([new PullRequestMove("api", DependentPr, "agent-smith/S1", "agent-smith/S3"),
            new PullRequestMove(Worker, DependentWorkerPr, "agent-smith/S1", "agent-smith/S2")]);
    }

    [Fact]
    public async Task RetargetAsync_ARepoNoUnlandedPrerequisiteTouched_MovesToTheDefaultBranch()
    {
        StackOnS1();

        (await Retarget().RetargetAsync(Of("client"), "42", Attempt(DependentPr, ("client", ClientPr)), default)).Should().Be(2);

        _moved.Should().ContainSingle(m => m.Repo == "client").Which.To.Should().Be("main", "no unlanded prerequisite has a client branch");
    }

    [Fact]
    public async Task RetargetAsync_APullRequestOnADeeperLandedPrerequisitesBranch_IsMovedToo()
    {
        _facts.Edges.Clear();
        _facts.Landed.Clear();
        _facts.Edges.AddRange([Edge("s2", "s1"), Edge("s3", "s2"), Edge("42", "s3")]);
        _facts.Land("s2");
        _facts.Land("s1");
        Worked("s2", Worker);
        _provider.Setup(p => p.ReadPullRequestBaseAsync(DependentWorkerPr, default)).ReturnsAsync("agent-smith/S2");
        _provider.Setup(p => p.ReadPullRequestBaseAsync($"https://git.test/o/{Worker}/pull/s2", default)).ReturnsAsync("main");

        (await Retarget().RetargetAsync(Of(Worker), "42", Attempt(DependentWorkerPr, (Worker, DependentWorkerPr)), default)).Should().Be(1);

        _moved.Should().Equal(new PullRequestMove(Worker, DependentWorkerPr, "agent-smith/S2", "main"));
    }

    private const string Worker = "worker";
    private const string DependentWorkerPr = "https://git.test/o/worker/pull/8";
    private const string ClientPr = "https://git.test/o/client/pull/8";

    private void StackOnS1()
    {
        _facts.Edges.Clear();
        _facts.Landed.Clear();
        _facts.Edges.AddRange([Edge("s2", "s1"), Edge("s3", "s2"), Edge("42", "s1"), Edge("42", "s2"), Edge("42", "s3")]);
        _facts.Land("s1");
        Worked("s1", "api", Worker);
        Worked("s2", "api", Worker);
        Worked("s3", "api");
        foreach (var pr in new[] { DependentPr, DependentWorkerPr, ClientPr })
            _provider.Setup(p => p.ReadPullRequestBaseAsync(pr, default)).ReturnsAsync("agent-smith/S1");
    }

    private void Worked(string ticket, params string[] repos) =>
        _attempts.Setup(a => a.LatestAsync(Project, ticket.ToUpperInvariant(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt($"run-{ticket}", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true)
            {
                OpenedPullRequest = true,
                PullRequestUrls = repos.ToDictionary(r => r, r => $"https://git.test/o/{r}/pull/{ticket}"),
            });

    private StackedPullRequestRetarget Retarget()
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        var scopes = new ServiceCollection().AddScoped(_ => _runs.Object).AddScoped<PullRequestRetargetRun>()
            .BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new(_facts, _facts, new GreenPrerequisites(_facts, _attempts.Object, _facts, _facts), _attempts.Object, _leases.Object, sources.Object, scopes, NullLogger<StackedPullRequestRetarget>.Instance);
    }

    private static PreviousAttempt Attempt(string url, params (string Repo, string Url)[] more) =>
        new("run", RunStatuses.Success, DateTimeOffset.UnixEpoch, Finished: true)
        {
            OpenedPullRequest = true,
            PullRequestUrls = more.Append(("api", url)).ToDictionary(p => p.Item1, p => p.Item2),
        };

    private static ResolvedProject Of(params string[] more) => new()
    {
        Name = Project, Tracker = new TrackerConnection { Name = Tracker, Type = TrackerType.Jira },
        Repos = [new RepoConnection { Name = "api" }, .. more.Select(r => new RepoConnection { Name = r, DefaultBranch = "main" })],
    };
}
