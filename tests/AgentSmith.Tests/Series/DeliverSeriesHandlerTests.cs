using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>
/// 2026-11-24-f9c5e: once every ticket landed on the series branch, each repo a ticket pull request merged into it gets one pull
/// request into the default branch; once the series check passed (Delivering), every repo merged ends the series and wakes the
/// tickets that waited for it. 2026-11-25-9992e: the step merges and refreshes nothing — the series master does.
/// </summary>
public sealed class DeliverSeriesHandlerTests
{
    private const string Branch = "agent-smith/series/sr-1";
    private const string Final = "https://github.com/example/api/pull/90";
    private readonly InMemorySeriesRunStore _store = new();
    private readonly Mock<ISourceProvider> _host = new();
    private readonly Mock<ISeriesRunLaunch> _launch = new();
    private readonly Mock<ISeriesTicketActs> _acts = new();
    private readonly List<string> _created = [];
    private readonly List<BranchPullRequest> _from = [];
    private string? _open;
    private readonly DateTimeOffset _now = SeriesRecords.T;

    private string? _tip;
    private bool _throws;

    public DeliverSeriesHandlerTests()
    {
        _host.As<IWorkBranchCleanup>().Setup(h => h.ListWorkBranchesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _tip is null ? [] : [new WorkBranchTip(Branch, _tip)]);
        _host.As<IWorkBranchCleanup>().Setup(h => h.PullRequestsOfAsync(Branch, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BranchPullRequests([.. _from], 0));
        _host.Setup(h => h.FindOpenPullRequestAsync(It.Is<Repository>(r => r.CurrentBranch.Value == Branch), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _throws ? throw new HttpRequestException("503") : _open);
        _host.Setup(h => h.CreatePullRequestAsync(It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<PullRequestTicket?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()))
            .Callback<Repository, string, string, CancellationToken, PullRequestTicket?, bool, BranchName?>((r, _, body, _, _, _, _) => _created.Add(body))
            .ReturnsAsync(Final);
        _acts.Setup(a => a.WakeDependentsAsync("sample", It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [Fact]
    public async Task EveryTicketLanded_OpensOnePullRequest_OnlyWhereATicketMergedIntoTheBranch()
    {
        var beat = await BeatAsync(SeriesRunStates.Running);

        _created.Should().ContainSingle().Which.Should().Contain("## SCRUM-43: work").And.Contain(Branch); // 2026-11-25-9992i
        beat.Said.Should().ContainSingle().Which.Should().Contain(Final).And.Contain("open for your review");
        _store.Records["sr-1"].Deliveries.Should().ContainSingle().Which.Should().Match<SeriesDelivery>(d => d.Repo == "api" && d.Number == "90");
        _host.Verify(h => h.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<BranchProtection>(), It.IsAny<MergeStyle>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnOpenPullRequest_IsAdopted_AndNotAllLandedOpensNone()
    {
        _open = Final;
        await BeatAsync(SeriesRunStates.Running);
        _created.Should().BeEmpty();

        await BeatAsync(SeriesRunStates.Running, landed: false);
        _created.Should().BeEmpty();
    }

    // 2026-11-25-9992e: an open final pull request — conflicting or not, in Delivering or not — is read only.
    [Theory]
    [InlineData(SeriesRunStates.Running)]
    [InlineData(SeriesRunStates.Delivering)]
    public async Task AnOpenFinalPullRequest_IsNeitherMergedNorRefreshedByTheStep(string state)
    {
        _open = Final;
        _from.Add(new BranchPullRequest(BranchPullRequestState.Open, "h1"));
        _host.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(Final, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("90", PrReviewStates.Open, SeriesRecords.T, []) { HasConflicts = true });

        await BeatAsync(state);

        _host.Verify(h => h.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<BranchProtection>(), It.IsAny<MergeStyle>(), It.IsAny<CancellationToken>()), Times.Never);
        _launch.VerifyNoOtherCalls();
        _store.Records["sr-1"].State.Should().Be(state);
        _store.Records["sr-1"].Deliveries.Single().Url.Should().Be(Final);
    }

    [Fact]
    public async Task AClosedFinalPullRequest_IsSaid_AndTheSeriesWaits()
    {
        await BeatAsync(SeriesRunStates.Running);
        _from.Add(new BranchPullRequest(BranchPullRequestState.Closed, "h1"));

        (await BeatAsync(SeriesRunStates.Delivering)).Said.Should().ContainSingle().Which.Should().Contain("closed without merging");
        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Delivering);
        _created.Should().ContainSingle("a closed one is never opened again by itself");
    }

    // Post-build review: a branch merged once and moved on (a follow-up landed) is delivered again; merged at its tip is done.
    [Fact]
    public async Task AMergedFinalPullRequest_CountsOnlyAtTheBranchTip()
    {
        _from.Add(new BranchPullRequest(BranchPullRequestState.Merged, "h1"));
        _tip = "h2";
        await BeatAsync(SeriesRunStates.Delivering);
        _created.Should().ContainSingle("the follow-up's work is not on the default branch yet");

        _tip = "h1";
        var beat = await BeatAsync(SeriesRunStates.Delivering);
        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Done);
        beat.Said.Should().Contain(s => s.Contains("it is done") && s.Contains("1 ticket(s) outside it"));
    }

    [Fact]
    public async Task ADifferentOpenPullRequest_ReplacesTheKnownOne_AndAFailingHostIsSaid()
    {
        _store.Records["sr-1"] = SeriesRecords.Running() with
        {
            IntegrationBranch = Branch, Deliveries = [new SeriesDelivery("api", "https://github.com/example/api/pull/80", "80")],
        };
        _open = Final;
        _from.Add(new BranchPullRequest(BranchPullRequestState.Open, "h1"));
        await BeatAsync(SeriesRunStates.Delivering);
        _store.Records["sr-1"].Deliveries.Single().Url.Should().Be(Final);

        _throws = true;
        var beat = await BeatAsync(SeriesRunStates.Delivering);
        beat.Said.Should().ContainSingle().Which.Should().Contain("could not be read or opened: 503");
    }

    [Fact]
    public async Task NoRepoToDeliver_NeverEndsTheSeries()
    {
        _store.Records["sr-1"] = SeriesRecords.Running() with { IntegrationBranch = Branch, Tickets = [SeriesRecords.Ticket("44")] };

        await BeatAsync(SeriesRunStates.Delivering);

        _store.Records["sr-1"].State.Should().Be(SeriesRunStates.Delivering);
    }

    private async Task<SeriesBeat> BeatAsync(string state, bool landed = true)
    {
        var known = _store.Records.GetValueOrDefault("sr-1");
        var record = (known ?? SeriesRecords.Running() with { IntegrationBranch = Branch }) with { State = state };
        _store.Records["sr-1"] = record;
        var beat = new SeriesBeat(record, [.. record.Tickets.Select(t => SeriesRecords.Fact(t.TicketId, landed: landed ? SeriesRecords.T : null))], _now);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "beat-1");
        pipeline.Set(ContextKeys.SeriesBeat, beat);
        var project = new ResolvedProject
        {
            Name = "sample",
            Repos = [new RepoConnection { Name = "api", Type = RepoType.GitHub, Url = "https://github.com/example/api" },
                new RepoConnection { Name = "web", Type = RepoType.GitHub, Url = "https://github.com/example/web" }],
        };
        await Handler().ExecuteAsync(new DeliverSeriesContext(pipeline, project), default);
        return beat;
    }

    private DeliverSeriesHandler Handler()
    {
        var sources = Mock.Of<ISourceProviderFactory>(f => f.Create(It.IsAny<RepoConnection>()) == _host.Object);
        var records = new Mock<IPrReviewRecordReader>();
        records.Setup(r => r.ByTicketAsync("sample", It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((string _, string ticket, CancellationToken _) =>
            ticket == "43" ? [new PrReviewRecordView("k", "https://github.com/example/api/pull/7", "api", "7", "sample", "43", "r", PrReviewStates.Merged,
                SeriesRecords.T, SeriesRecords.T, null, []) { TargetBranch = Branch }] : []);
        var scopes = new ServiceCollection().AddSingleton(records.Object).BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new DeliverSeriesHandler(_store, new SeriesFinalPullRequest(sources), _acts.Object, scopes);
    }
}
