using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>
/// 2026-11-17-023fa: the merge step merges only the pull requests CommitAndPR published as gate-passed, in the protection the
/// context names, stops at the first refusal, leaves a rung-targeted pull request open, and records and says every outcome.
/// 2026-11-25-9992i: a code run merges nothing itself — under review: none its pull requests wait for CI, under end and each they
/// stay open for a person; the merge-green run merges.
/// </summary>
public sealed class CompletePullRequestsHandlerTests
{
    private readonly List<(string Url, BranchProtection Mode)> _merged = [];
    private readonly List<MergeStyle> _styles = []; // 2026-11-25-9992i
    private readonly List<PullRequestOutcomeEvent> _outcomes = [];
    private readonly Dictionary<string, PullRequestCompletion> _answers = new();
    private readonly Mock<ITicketRequirementStore> _edges = new();
    private readonly Mock<ITicketLandingStore> _landings = new();
    private readonly AgentSmith.Tests.Series.InMemorySeriesRunStore _series = new(); // 2026-11-25-9992e
    private readonly Dictionary<string, string?> _bases = new();
    private readonly List<string> _calls = []; // 2026-11-23-8d16: ready, approve and merge in the order they were asked
    private bool _ready = true;
    private bool _draft = true;

    public CompletePullRequestsHandlerTests()
    {
        _edges.Setup(e => e.RequiredOfAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _landings.Setup(r => r.LandedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task MergeGreen_MergesEach_InTheContextsProtection_AndRecordsIt()
    {
        var result = await RunAsync(OnCi(respect: true), passed: null, Green(["a", "b"], null, null));

        _merged.Should().Equal(("https://x/a/pull/1", BranchProtection.Respect), ("https://x/b/pull/1", BranchProtection.Respect));
        _outcomes.Should().AllSatisfy(o => o.Status.Should().Be(PullRequestStatuses.Completed));
        result.Message.Should().Contain("a: merged").And.Contain("b: merged");
    }

    [Fact]
    public async Task FirstRefusal_StopsTheRest_AndNamesTheReason()
    {
        _answers["https://x/a/pull/1"] = PullRequestCompletion.Refused(
            "cannot bypass branch protection on GitHub: review required; grant admin, or merge with how auto so the host's rules decide");

        var result = await RunAsync(OnCi(), passed: null, Green(["a", "b"], null, null));

        _merged.Select(m => m.Url).Should().Equal("https://x/a/pull/1");
        _merged.Single().Mode.Should().Be(BranchProtection.Bypass, "the context names the protection");
        result.Message.Should().Contain("a: still open — cannot bypass branch protection");
        result.Message.Should().Contain("b: not attempted");
        _outcomes.First().Status.Should().Be(PullRequestStatuses.CompletionRefused);
    }

    [Fact]
    public async Task Armed_IsNotARefusal()
    {
        _answers["https://x/a/pull/1"] = PullRequestCompletion.Armed("waiting on the build policy");

        var result = await RunAsync(OnCi(respect: true), passed: null, Green(["a", "b"], null, null));

        _merged.Should().HaveCount(2);
        result.Message.Should().Contain("a: armed — waiting on the build policy");
        _outcomes.First().Status.Should().Be(PullRequestStatuses.CompletionArmed);
    }

    // 2026-11-25-9992i: a merge into a series branch squashes; one into the default branch keeps the host's method.
    [Fact]
    public async Task MergeGreen_IntoASeriesBranch_Squashes_ElseTheHostsMethod()
    {
        await RunAsync(OnCi(), passed: null, Green("a", askedBy: "series master"), "agent-smith/series/sr-1");
        await RunAsync(OnCi(), passed: null, Green("a", askedBy: "series master"));

        _styles.Should().Equal(MergeStyle.Squash, MergeStyle.HostDefault);
    }

    // 2026-11-25-9992i: the builder carries the project's review; a series-branch run merges past protection, every other merge
    // under the host's branch protection, and a judged request under the protection it names.
    [Fact]
    public void Builder_CarriesTheReview_AndPicksTheProtection()
    {
        var project = new ResolvedProject
        {
            Name = "p", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub }, Review = ReviewMode.None,
        };
        var stamped = new PipelineContext();
        stamped.Set(ContextKeys.SeriesBranch, "agent-smith/series/sr-1");
        var builder = new AgentSmith.Application.Services.Builders.CompletePullRequestsContextBuilder();

        var series = (CompletePullRequestsContext)builder.Build(null!, project, stamped);
        var plain = (CompletePullRequestsContext)builder.Build(null!, project, new PipelineContext());

        (series.Review, series.Protection, series.SeriesBranch).Should().Be((ReviewMode.None, BranchProtection.Bypass, "agent-smith/series/sr-1"));
        (plain.Review, plain.Protection, plain.SeriesBranch).Should().Be((ReviewMode.None, BranchProtection.Respect, (string?)null));
        (series.Style, plain.Style).Should().Be((MergeStyle.Squash, MergeStyle.HostDefault), "2026-11-25-9992i: one commit per ticket on a series branch");
    }

    // 2026-11-23-31dce
    [Fact]
    public async Task EndReview_APersonAskedMerge_Runs_AndFillsTheOutcomeSlot()
    {
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var result = await RunAsync(EndReview(), passed: null, Green("a", "b", askedBy: "Dana", slot: slot));

        _merged.Should().HaveCount(2);
        result.Message.Should().StartWith("asked by Dana").And.Contain("a: merged");
        slot.Value!.Select(l => (l.Repo, l.Outcome)).Should().Equal(
            ("a", PullRequestCompletionOutcome.Merged), ("b", PullRequestCompletionOutcome.Merged));
    }

    [Fact]
    public async Task MergeGreen_ABaseOnAPrerequisitesBranch_IsRefusedInsideTheRun_AndStopsTheRest()
    {
        _bases["https://x/a/pull/1"] = "agent-smith/41";
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var result = await RunAsync(EndReview(), passed: null, Green("a", "b", askedBy: "Dana", slot: slot));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: targets 'agent-smith/41', not the default branch").And.Contain("b: not attempted");
        slot.Value!.Should().AllSatisfy(l => l.Outcome.Should().Be(PullRequestCompletionOutcome.Refused));
        _outcomes.Select(o => (o.RunId, o.Status)).Should().Equal([("run-1", PullRequestStatuses.CompletionRefused)],
            "a person's refused merge leaves the attempt's record as it was");
    }

    [Fact]
    public async Task MergeGreen_AnUnreadableHostedBase_IsRefused()
    {
        _bases["https://x/a/pull/1"] = null;

        var result = await RunAsync(OnCi(), passed: null, Green("a"));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: its base could not be read from the host");
    }

    [Fact]
    public async Task EndReview_APersonAskedMerge_StillMeetsTheUnlandedRequirementGuard()
    {
        Requires("41");

        var result = await RunAsync(EndReview(), passed: null, Green("a", askedBy: "Dana"));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: not merged: #41, a prerequisite, is not merged into the default branch yet");
        _outcomes.Should().ContainSingle().Which.RunId.Should().Be("run-1");
    }

    [Fact]
    public async Task MergeGreen_AHostRefusal_CarriesTheHostsReasonIntoTheSlot()
    {
        _answers["https://x/a/pull/1"] = PullRequestCompletion.Refused("the branch policy requires an approval");
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        await RunAsync(EndReview(), passed: null, Green("a", askedBy: "Dana", slot: slot));

        slot.Value!.Single().Should().Be(new PullRequestMergeLine("a", PullRequestCompletionOutcome.Refused,
            "a: still open — the branch policy requires an approval"));
    }

    // 2026-11-23-8d16
    [Fact]
    public async Task MergeGreen_APersonAskedMerge_TakesEachOutOfDraft_BeforeItMerges()
    {
        await RunAsync(EndReview(), passed: null, Green("a", askedBy: "Dana"));

        _calls.Should().Equal("ready https://x/a/pull/1", "requeue-unsettled https://x/a/pull/1", "merge https://x/a/pull/1");
    }

    // 2026-11-23-0219: a person's merge queues again the required builds that have not started, after the draft step and before
    // the approval and the arm; the CI watch's own merge never does.
    [Fact]
    public async Task MergeGreen_AnAskedMerge_RequeuesNotStartedBuilds()
    {
        await RunAsync(EndReview(), passed: null, Green("a", askedBy: "Dana"));
        _calls.Should().Contain("requeue-unsettled https://x/a/pull/1");
    }

    [Fact]
    public async Task MergeGreen_ADraftTheHostKeeps_IsRefused_AndNothingMerges()
    {
        _ready = false;
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var result = await RunAsync(EndReview(), passed: null, Green("a", "b", askedBy: "Dana", slot: slot));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: still a draft: the host did not take it out of draft").And.Contain("b: not attempted");
        slot.Value!.Should().AllSatisfy(l => l.Outcome.Should().Be(PullRequestCompletionOutcome.Refused));
    }

    [Fact]
    public async Task MergeGreen_APersonAskedMerge_LeavesAPullRequestThatIsNoDraft_Alone()
    {
        _draft = false;

        await RunAsync(EndReview(), passed: null, Green("a", askedBy: "Dana"));

        _calls.Should().Equal("requeue-unsettled https://x/a/pull/1", "merge https://x/a/pull/1");
    }

    // 2026-11-24-629aa: the CI loop's request for a build that has not started queues it again and merges nothing, under every review.
    [Theory]
    [InlineData(ReviewMode.End)]
    [InlineData(ReviewMode.None)]
    public async Task MergeGreen_ANotStartedRequest_QueuesTheBuildsAgain_AndMergesNothing(ReviewMode review)
    {
        var result = await RunAsync((review, BranchProtection.Respect), passed: null, pipeline =>
            pipeline.Set(ContextKeys.CiGreenMerge, new CiGreenMergeRequest("42", "attempt-1", "agent-smith/42",
                [new CiGreenPullRequest("a", "https://x/a/pull/1") { NotStarted = true }, new CiGreenPullRequest("c", "https://x/c/pull/1") { NotStarted = true }])));

        _calls.Should().Equal("requeue https://x/a/pull/1");
        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: 1 queued").And.Contain("c: 0 queued").And.Contain("nothing merges");
    }

    // 2026-11-25-9992e: a merge the series master (or a person) judged passes the prerequisite, rung and base refusals.
    [Fact]
    public async Task MergeGreen_AJudgedMerge_WithAnUnlandedPrerequisiteAndABaseOffMain_Merges()
    {
        Requires("41");
        _bases["https://x/a/pull/1"] = "agent-smith/41";

        var result = await RunAsync(EndReview(), passed: null, pipeline => pipeline.Set(ContextKeys.CiGreenMerge,
            new CiGreenMergeRequest("42", "attempt-1", "agent-smith/42", [new CiGreenPullRequest("a", "https://x/a/pull/1")])
            { AskedBy = "series master", Judged = true }));

        _merged.Should().Equal(("https://x/a/pull/1", BranchProtection.Respect));
        result.Message.Should().Contain("a: merged");
    }

    // 2026-11-25-96aeb: the series master's merge is checked — an unlanded prerequisite refuses, overridably, naming its target.
    [Fact]
    public async Task MergeGreen_AMasterMerge_WithAnUnlandedPrerequisite_IsRefusedOverridably_AndMarksNoAttempt()
    {
        Requires("41");

        var result = await RunAsync(EndReview(), passed: null, pipeline => pipeline.Set(ContextKeys.CiGreenMerge, Master()));

        _merged.Should().BeEmpty();
        _outcomes.Should().OnlyContain(o => o.RunId == "run-1", "an asked merge marks a refusal on its own run only");
        result.Message.Should().Contain("a: not merged: #41, a prerequisite, is not merged into the default branch yet");
    }

    [Fact]
    public async Task MergeGreen_AMasterMergeWithAnOverride_Merges_AndNamesTheRefusalItWentPast()
    {
        Requires("41");
        _bases["https://x/a/pull/1"] = null;
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        await RunAsync(EndReview(), passed: null, pipeline =>
        {
            pipeline.Set(ContextKeys.CiGreenMerge, Master() with { Judged = true, OverrideReason = "41 only renames a field" });
            pipeline.Set(ContextKeys.ActOutcome, slot);
        });

        _merged.Should().ContainSingle();
        slot.Value.Should().ContainSingle().Which.Overrode.Should().Equal(
            "not merged: #41, a prerequisite, is not merged into the default branch yet", PullRequestBaseGuard.Unread);
    }

    [Fact]
    public async Task MergeGreen_AMasterMergeOntoAForeignBase_IsRefusedEvenWithAnOverride()
    {
        _bases["https://x/a/pull/1"] = "agent-smith/41";
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        await RunAsync(EndReview(), passed: null, pipeline =>
        {
            pipeline.Set(ContextKeys.CiGreenMerge, Master() with { Judged = true, OverrideReason = "land it" });
            pipeline.Set(ContextKeys.ActOutcome, slot);
        });

        _merged.Should().BeEmpty();
        slot.Value.Should().ContainSingle().Which.Should().Match<PullRequestMergeLine>(l => !l.Overridable && l.Text.Contains("targets 'agent-smith/41'"));
    }

    [Fact]
    public void PrerequisiteWords_APrerequisiteOfTheSeries_LandsOnItsBranch_AnyOtherOnTheDefaultBranch()
    {
        TicketRequirementEdge Edge(string id) => new("gh", "github-42", "42", "p", $"github-{id}", id, false, DateTimeOffset.UnixEpoch, "c-1");

        PullRequestPrerequisiteWords.Refusal([Edge("40"), Edge("41")], "agent-smith/series/sr-1", ["40"]).Should().Be(
            "not merged: #40, a prerequisite, is not merged into the series branch 'agent-smith/series/sr-1' yet; "
            + "#41, a prerequisite, is not merged into the default branch yet");
    }

    private static CiGreenMergeRequest Master() =>
        new("42", "attempt-1", "agent-smith/42", [new CiGreenPullRequest("a", "https://x/a/pull/1")]) { AskedBy = "series master", ChecksTarget = true };

    [Fact]
    public async Task MergeGreen_ACiStartRequest_MergesNothing()
    {
        var result = await RunAsync(EndReview(), passed: null, pipeline => pipeline.Set(ContextKeys.CiGreenMerge,
            new CiGreenMergeRequest("42", "attempt-1", "agent-smith/42", [new CiGreenPullRequest("a", "https://x/a/pull/1") { NoCi = true }])
            { AskedBy = "series master" }));

        _merged.Should().BeEmpty();
        result.Message.Should().EndWith("nothing merges.");
    }

    private void Requires(params string[] required) =>
        _edges.Setup(e => e.RequiredOfAsync("gh", "github-42", It.IsAny<CancellationToken>())).ReturnsAsync([.. required.Select(r =>
            new TicketRequirementEdge("gh", "github-42", "42", "p", $"github-{r}", r, true, DateTimeOffset.UnixEpoch, "c-1"))]);

    private static Action<PipelineContext> Green(string repo, string? askedBy = null, ActSlot<IReadOnlyList<PullRequestMergeLine>>? slot = null) =>
        Green([repo], askedBy, slot);

    private static Action<PipelineContext> Green(string first, string second, string? askedBy = null,
        ActSlot<IReadOnlyList<PullRequestMergeLine>>? slot = null) => Green([first, second], askedBy, slot);

    private static Action<PipelineContext> Green(string[] repos, string? askedBy, ActSlot<IReadOnlyList<PullRequestMergeLine>>? slot) => pipeline =>
    {
        pipeline.Set(ContextKeys.CiGreenMerge, new CiGreenMergeRequest("42", "attempt-1", "agent-smith/42",
            [.. repos.Select(r => new CiGreenPullRequest(r, $"https://x/{r}/pull/1"))]) { AskedBy = askedBy ?? "series master" }); // 96aea: only an asked merge merges
        if (slot is not null) pipeline.Set(ContextKeys.ActOutcome, slot);
    };

    // 2026-11-25-9992i: review: none, the protection the context names.
    private static (ReviewMode, BranchProtection) OnCi(bool respect = false) =>
        (ReviewMode.None, respect ? BranchProtection.Respect : BranchProtection.Bypass);

    // A project left at review: end, merging under the host's branch protection.
    private static (ReviewMode, BranchProtection) EndReview() => (ReviewMode.End, BranchProtection.Respect);

    private static List<OpenedPullRequest> Passed(params string[] repos) =>
        [.. repos.Select(r => new OpenedPullRequest(r, $"https://x/{r}/pull/1", OpenStatus.Opened))];

    private async Task<CommandResult> RunAsync(
        (ReviewMode Review, BranchProtection Protection) policy, List<OpenedPullRequest>? passed, Action<PipelineContext>? arrange = null,
        string? seriesBranch = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        if (passed is not null) pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests, passed);
        arrange?.Invoke(pipeline);
        var repos = new[] { "a", "b" }.Select(n => new RepoConnection { Name = n, Type = RepoType.GitHub }).ToList();
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<BranchProtection>(), It.IsAny<MergeStyle>(), It.IsAny<CancellationToken>()))
            .Returns<string, BranchName, BranchProtection, MergeStyle, CancellationToken>((url, _, mode, style, _) =>
            {
                _styles.Add(style);
                _merged.Add((url, mode));
                _calls.Add($"merge {url}");
                return Task.FromResult(_answers.GetValueOrDefault(url) ?? PullRequestCompletion.Merged());
            });
        provider.Setup(p => p.ReadPullRequestBaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) => _bases.TryGetValue(url, out var at) ? at : "main");
        provider.As<AgentSmith.Contracts.Reviews.IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) =>
                new AgentSmith.Contracts.Reviews.PrReviewState("1", "open", DateTimeOffset.UnixEpoch, []) { IsDraft = _draft });
        provider.Setup(p => p.MarkPullRequestReadyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((url, _) =>
            {
                _calls.Add($"ready {url}");
                return Task.FromResult(_ready);
            });
        provider.As<AgentSmith.Contracts.Reviews.IPullRequestBuildRequeuer>()
            .Setup(r => r.RequeueNotStartedAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns<string, bool, CancellationToken>((url, settled, _) =>
            {
                _calls.Add(settled ? $"requeue {url}" : $"requeue-unsettled {url}"); // 2026-11-25-7a92b
                return Task.FromResult(1);
            });
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => { if (e is PullRequestOutcomeEvent o) _outcomes.Add(o); })
            .Returns(Task.CompletedTask);
        var completer = new PullRequestCompleter(factory.Object, events.Object, NullLogger<PullRequestCompleter>.Instance);
        var handler = new CompletePullRequestsHandler(new PullRequestMergeSequence(completer,
            new PullRequestBaseGuard(factory.Object), NullLogger<PullRequestMergeSequence>.Instance,
            new UnmetTicketRequirements(_edges.Object, _landings.Object, Mock.Of<ISpecApprovalStore>()),
            new PullRequestDraftStep(factory.Object),
            new PullRequestBuildRequeue(factory.Object, NullLogger<PullRequestBuildRequeue>.Instance)),
            requeue: new PullRequestBuildRequeue(factory.Object, NullLogger<PullRequestBuildRequeue>.Instance));
        return await handler.ExecuteAsync(new CompletePullRequestsContext(policy.Review, policy.Protection, new BranchName("agent-smith/42"), repos, pipeline,
            "gh", "github-42", seriesBranch)
        {
            Project = "p", TicketId = "42",
            Style = AgentSmith.Application.Services.Builders.CompletePullRequestsContextBuilder.Style(seriesBranch),
        }, CancellationToken.None);
    }
}
