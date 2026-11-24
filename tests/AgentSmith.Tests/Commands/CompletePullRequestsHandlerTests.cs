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
/// 2026-11-17-023fa: a green code run merges its own pull requests only when the project says on_gate_green and
/// only those CommitAndPR published as gate-passed; it merges in the project's protection mode, stops at the first
/// refusal, leaves a rung-targeted pull request open, and records and says every outcome.
/// </summary>
public sealed class CompletePullRequestsHandlerTests
{
    private readonly List<(string Url, BranchProtection Mode)> _merged = [];
    private readonly List<PullRequestOutcomeEvent> _outcomes = [];
    private readonly Dictionary<string, PullRequestCompletion> _answers = new();
    private readonly List<string> _approved = [];
    private PullRequestApproval _approval = PullRequestApproval.Approved;
    private readonly Mock<ITicketRequirementStore> _edges = new();
    private readonly Mock<ITicketLandingStore> _landings = new();
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
    public async Task Manual_MergesNothing()
    {
        var result = await RunAsync(new MergePolicyConfig(), Passed("a"));

        result.Message.Should().Contain("merge: manual");
        _merged.Should().BeEmpty();
    }

    [Fact]
    public async Task NoGatePassedPullRequests_MergesNothing_AndSaysWhy()
    {
        var result = await RunAsync(OnGreen(), passed: null);

        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Contain("did not pass its gate");
        _merged.Should().BeEmpty();
    }

    [Fact]
    public async Task OnGateGreen_MergesEach_InTheProjectsMode_AndRecordsIt()
    {
        var result = await RunAsync(OnGreen(respect: true), Passed("a", "b"));

        _merged.Should().Equal(("https://x/a/pull/1", BranchProtection.Respect), ("https://x/b/pull/1", BranchProtection.Respect));
        _outcomes.Should().AllSatisfy(o => o.Status.Should().Be(PullRequestStatuses.Completed));
        result.Message.Should().Contain("a: merged").And.Contain("b: merged");
    }

    [Fact]
    public async Task FirstRefusal_StopsTheRest_AndNamesTheReason()
    {
        _answers["https://x/a/pull/1"] = PullRequestCompletion.Refused(
            "cannot bypass branch protection on GitHub: review required; grant admin or set respect_branch_protection: true");

        var result = await RunAsync(OnGreen(), Passed("a", "b"));

        _merged.Select(m => m.Url).Should().Equal("https://x/a/pull/1");
        _merged.Single().Mode.Should().Be(BranchProtection.Bypass, "respect_branch_protection defaults to false");
        result.Message.Should().Contain("a: still open — cannot bypass branch protection");
        result.Message.Should().Contain("b: not attempted");
        _outcomes.Single().Status.Should().Be(PullRequestStatuses.CompletionRefused);
    }

    [Fact]
    public async Task Armed_IsNotARefusal()
    {
        _answers["https://x/a/pull/1"] = PullRequestCompletion.Armed("waiting on the build policy");

        var result = await RunAsync(OnGreen(respect: true), Passed("a", "b"));

        _merged.Should().HaveCount(2);
        result.Message.Should().Contain("a: armed — waiting on the build policy");
        _outcomes.First().Status.Should().Be(PullRequestStatuses.CompletionArmed);
    }

    [Fact]
    public async Task RungTarget_IsLeftOpen_AndNamed()
    {
        var result = await RunAsync(OnGreen(), Passed("a"), pipeline => PullRequestTargets.Record(pipeline, "a", "feature/epic"));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("opened against 'feature/epic'");
        _outcomes.Single().Status.Should().Be(PullRequestStatuses.CompletionRefused);
    }

    // 2026-11-24-f9c5c: a series ticket's pull request onto its series branch merges under the series policy (past protection);
    // one onto another branch is still a rung refusal.
    [Fact]
    public async Task SeriesBranchTarget_MergesUnderSeriesWhen_PastProtection()
    {
        var policy = new MergePolicyConfig { When = MergeWhen.Manual, SeriesWhen = MergeWhen.OnGateGreen, RespectBranchProtection = true };
        const string series = "agent-smith/series/sr-1";

        var result = await RunAsync(policy.IntoSeriesBranch(), Passed("a"), p => PullRequestTargets.Record(p, "a", series), series);

        _merged.Should().Equal(("https://x/a/pull/1", BranchProtection.Bypass));
        result.Message.Should().Contain("a: merged");
    }

    [Fact]
    public async Task SeriesBranchRun_APullRequestOntoAnotherRung_IsStillLeftOpen()
    {
        var result = await RunAsync(OnGreen(), Passed("a"), p => PullRequestTargets.Record(p, "a", "agent-smith/41"), "agent-smith/series/sr-1");

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("opened against 'agent-smith/41'");
    }

    [Fact]
    public void Builder_ARunStampedWithASeriesBranch_MergesUnderTheSeriesPolicy()
    {
        var project = new ResolvedProject
        {
            Name = "p", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            Merge = new MergePolicyConfig { When = MergeWhen.OnGateGreen, SeriesWhen = MergeWhen.Manual, RespectBranchProtection = true },
        };
        var stamped = new PipelineContext();
        stamped.Set(ContextKeys.SeriesBranch, "agent-smith/series/sr-1");
        var builder = new AgentSmith.Application.Services.Builders.CompletePullRequestsContextBuilder();

        var series = (CompletePullRequestsContext)builder.Build(null!, project, stamped);
        var plain = (CompletePullRequestsContext)builder.Build(null!, project, new PipelineContext());

        (series.Policy.When, series.Policy.Protection, series.SeriesBranch).Should().Be((MergeWhen.Manual, BranchProtection.Bypass, "agent-smith/series/sr-1"));
        (plain.Policy.When, plain.SeriesBranch).Should().Be((MergeWhen.OnGateGreen, (string?)null));
    }

    // 2026-11-17-3435b
    [Fact]
    public async Task OnCiGreen_CodeRun_RecordsAwaitingCi_AndMergesNothing()
    {
        var result = await RunAsync(OnCi(), Passed("a", "b"));

        _merged.Should().BeEmpty();
        _outcomes.Should().HaveCount(2).And.AllSatisfy(o => o.Status.Should().Be(PullRequestStatuses.AwaitingCi));
        result.Message.Should().Contain("a, b wait for CI");
    }

    [Fact]
    public async Task OnCiGreen_MergeGreenRun_ApprovesInRespect_MergesAndTellsTheAttempt()
    {
        var result = await RunAsync(OnCi(respect: true), passed: null, Green("a"));

        _approved.Should().Equal("https://x/a/pull/1");
        _merged.Should().Equal(("https://x/a/pull/1", BranchProtection.Respect));
        _outcomes.Select(o => (o.RunId, o.Status)).Should().Equal(
            ("run-1", PullRequestStatuses.Completed), ("attempt-1", PullRequestStatuses.Completed));
        result.Message.Should().Contain("CI green").And.Contain("a: merged");
    }

    [Fact]
    public async Task OnCiGreen_Bypass_NeedsNoApproval()
    {
        await RunAsync(OnCi(), passed: null, Green("a"));

        _approved.Should().BeEmpty();
        _merged.Should().ContainSingle();
    }

    [Fact]
    public async Task OnCiGreen_RefusedApproval_LeavesItOpen_NamingTheKey()
    {
        _approval = PullRequestApproval.Refused("Can not approve your own pull request");

        var result = await RunAsync(OnCi(respect: true), passed: null, Green("a"));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("Can not approve your own pull request").And.Contain("merge.approval_auth");
        _outcomes.Should().AllSatisfy(o => o.Status.Should().Be(PullRequestStatuses.CompletionRefused));
    }

    // 2026-11-23-97c2a
    [Fact]
    public async Task OnGateGreen_ARequiredTicketNotLanded_MergesNothing_AndEachRepoRecordsTheRefusal()
    {
        Requires("41");

        var result = await RunAsync(OnGreen(), Passed("a", "b"));

        _merged.Should().BeEmpty();
        _outcomes.Should().HaveCount(2).And.AllSatisfy(o =>
        {
            o.Status.Should().Be(PullRequestStatuses.CompletionRefused);
            o.Reason.Should().Be("not merged: #41 it requires has not landed");
        });
        result.Message.Should().Contain("a: not merged: #41 it requires has not landed")
            .And.Contain("b: not merged: #41 it requires has not landed");
    }

    [Fact]
    public async Task OnCiGreen_MergeGreenRun_ARequiredTicketNotLanded_IsRefused_AndTheAttemptKeepsAwaitingCi()
    {
        Requires("40", "41");

        var result = await RunAsync(OnCi(respect: true), passed: null, Green("a"));

        _merged.Should().BeEmpty();
        _approved.Should().BeEmpty("nothing is approved for a merge that is refused");
        _outcomes.Select(o => (o.RunId, o.Status, o.Reason)).Should().Equal(
            ("run-1", PullRequestStatuses.CompletionRefused, "not merged: #40 and #41 it requires have not landed"),
            ("attempt-1", PullRequestStatuses.AwaitingCi, "not merged: #40 and #41 it requires have not landed"));
        result.Message.Should().Contain("a: not merged: #40 and #41 it requires have not landed");
    }

    [Fact]
    public async Task ARequiredTicketLanded_MergesAsBefore()
    {
        Requires("41");
        _landings.Setup(r => r.LandedAsync("gh", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TicketLandingFact("gh", "github-41", "41", "p", TicketLandingKinds.Merged, DateTimeOffset.UnixEpoch, null, null, null)]);

        await RunAsync(OnGreen(), Passed("a"));

        _merged.Should().ContainSingle();
    }

    [Fact]
    public async Task NoRequirementRows_MergesAsBefore_WithOneRequirementReadAndNoLandingRead()
    {
        await RunAsync(OnGreen(), Passed("a"));

        _merged.Should().ContainSingle();
        _edges.Verify(e => e.RequiredOfAsync("gh", "github-42", It.IsAny<CancellationToken>()), Times.Once);
        _landings.Verify(r => r.LandedAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ARequiredTicketNotLanded_ARungTarget_StillNamesItsRung()
    {
        Requires("41");

        var result = await RunAsync(OnGreen(), Passed("a", "b"), pipeline => PullRequestTargets.Record(pipeline, "a", "agent-smith/41"));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: opened against 'agent-smith/41'").And.Contain("b: not merged: #41 it requires has not landed");
    }

    // 2026-11-23-31dce
    [Fact]
    public async Task Manual_APersonAskedMerge_Runs_AndFillsTheOutcomeSlot()
    {
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var result = await RunAsync(new MergePolicyConfig(), passed: null, Green("a", "b", askedBy: "Dana", slot: slot));

        _merged.Should().HaveCount(2);
        result.Message.Should().StartWith("asked by Dana").And.Contain("a: merged");
        slot.Value!.Select(l => (l.Repo, l.Outcome)).Should().Equal(
            ("a", PullRequestCompletionOutcome.Merged), ("b", PullRequestCompletionOutcome.Merged));
    }

    [Fact]
    public async Task Manual_TheCiWatchsMergeGreenRequest_StillMergesNothing()
    {
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var result = await RunAsync(new MergePolicyConfig(), passed: null, Green("a", slot: slot));

        result.Message.Should().Contain("merge: manual");
        _merged.Should().BeEmpty();
        slot.IsSet.Should().BeFalse();
    }

    [Fact]
    public async Task MergeGreen_ABaseOnAPrerequisitesBranch_IsRefusedInsideTheRun_AndStopsTheRest()
    {
        _bases["https://x/a/pull/1"] = "agent-smith/41";
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var result = await RunAsync(new MergePolicyConfig(), passed: null, Green("a", "b", askedBy: "Dana", slot: slot));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: targets 'agent-smith/41', not the default branch").And.Contain("b: not attempted");
        slot.Value!.Should().AllSatisfy(l => l.Outcome.Should().Be(PullRequestCompletionOutcome.Refused));
        _outcomes.Select(o => (o.RunId, o.Status)).Should().Equal([("run-1", PullRequestStatuses.CompletionRefused)],
            "a person's refused merge leaves the attempt's record as it was");
    }

    [Fact]
    public async Task OnCiGreen_TheCiWatchsMerge_ABaseOffTheDefaultBranch_KeepsTheAttemptAwaitingCi()
    {
        _bases["https://x/a/pull/1"] = "agent-smith/41";

        await RunAsync(OnCi(), passed: null, Green("a"));

        _merged.Should().BeEmpty();
        _outcomes.Select(o => (o.RunId, o.Status)).Should().Equal(
            ("run-1", PullRequestStatuses.CompletionRefused), ("attempt-1", PullRequestStatuses.AwaitingCi));
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
    public async Task Manual_APersonAskedMerge_StillMeetsTheUnlandedRequirementGuard()
    {
        Requires("41");

        var result = await RunAsync(new MergePolicyConfig(), passed: null, Green("a", askedBy: "Dana"));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: not merged: #41 it requires has not landed");
        _outcomes.Should().ContainSingle().Which.RunId.Should().Be("run-1");
    }

    [Fact]
    public async Task MergeGreen_AHostRefusal_CarriesTheHostsReasonIntoTheSlot()
    {
        _answers["https://x/a/pull/1"] = PullRequestCompletion.Refused("the branch policy requires an approval");
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        await RunAsync(new MergePolicyConfig(), passed: null, Green("a", askedBy: "Dana", slot: slot));

        slot.Value!.Single().Should().Be(new PullRequestMergeLine("a", PullRequestCompletionOutcome.Refused,
            "a: still open — the branch policy requires an approval"));
    }

    // 2026-11-23-8d16
    [Fact]
    public async Task MergeGreen_APersonAskedMerge_TakesEachOutOfDraft_BeforeItApprovesAndMerges()
    {
        await RunAsync(new MergePolicyConfig { RespectBranchProtection = true }, passed: null, Green("a", askedBy: "Dana"));

        _calls.Should().Equal("ready https://x/a/pull/1", "requeue https://x/a/pull/1", "approve https://x/a/pull/1", "merge https://x/a/pull/1");
    }

    // 2026-11-23-0219: a person's merge queues again the required builds that have not started, after the draft step and before
    // the approval and the arm; the CI watch's own merge never does.
    [Fact]
    public async Task MergeGreen_APersonAskedMerge_RequeuesNotStartedBuilds_TheCiMergeDoesNot()
    {
        await RunAsync(new MergePolicyConfig { RespectBranchProtection = true }, passed: null, Green("a", askedBy: "Dana"));
        _calls.Should().Contain("requeue https://x/a/pull/1");

        _calls.Clear();
        await RunAsync(OnCi(respect: true), passed: null, Green("a"));
        _calls.Should().NotContain(c => c.StartsWith("requeue", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MergeGreen_ADraftTheHostKeeps_IsRefused_AndNothingMerges()
    {
        _ready = false;
        var slot = new ActSlot<IReadOnlyList<PullRequestMergeLine>>();

        var result = await RunAsync(new MergePolicyConfig(), passed: null, Green("a", "b", askedBy: "Dana", slot: slot));

        _merged.Should().BeEmpty();
        result.Message.Should().Contain("a: still a draft: the host did not take it out of draft").And.Contain("b: not attempted");
        slot.Value!.Should().AllSatisfy(l => l.Outcome.Should().Be(PullRequestCompletionOutcome.Refused));
    }

    [Fact]
    public async Task MergeGreen_APersonAskedMerge_LeavesAPullRequestThatIsNoDraft_Alone()
    {
        _draft = false;

        await RunAsync(new MergePolicyConfig(), passed: null, Green("a", askedBy: "Dana"));

        _calls.Should().Equal("requeue https://x/a/pull/1", "merge https://x/a/pull/1");
    }

    [Fact]
    public async Task OnCiGreen_TheCiWatchsMerge_NeverTakesADraftOut()
    {
        await RunAsync(OnCi(), passed: null, Green("a"));

        _calls.Should().NotContain(c => c.StartsWith("ready", StringComparison.Ordinal));
        _merged.Should().ContainSingle();
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
            [.. repos.Select(r => new CiGreenPullRequest(r, $"https://x/{r}/pull/1"))]) { AskedBy = askedBy });
        if (slot is not null) pipeline.Set(ContextKeys.ActOutcome, slot);
    };

    private static MergePolicyConfig OnCi(bool respect = false) =>
        new() { When = MergeWhen.OnCiGreen, RespectBranchProtection = respect };

    private static MergePolicyConfig OnGreen(bool respect = false) =>
        new() { When = MergeWhen.OnGateGreen, RespectBranchProtection = respect };

    private static List<OpenedPullRequest> Passed(params string[] repos) =>
        [.. repos.Select(r => new OpenedPullRequest(r, $"https://x/{r}/pull/1", OpenStatus.Opened))];

    private async Task<CommandResult> RunAsync(
        MergePolicyConfig policy, List<OpenedPullRequest>? passed, Action<PipelineContext>? arrange = null, string? seriesBranch = null)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.RunId, "run-1");
        if (passed is not null) pipeline.Set<IReadOnlyList<OpenedPullRequest>>(ContextKeys.GatePassedPullRequests, passed);
        arrange?.Invoke(pipeline);
        var repos = new[] { "a", "b" }.Select(n => new RepoConnection { Name = n, Type = RepoType.GitHub }).ToList();
        var provider = new Mock<ISourceProvider>();
        provider.As<IPullRequestApprover>().Setup(a => a.ApproveIfRequiredAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((url, _) =>
            {
                _approved.Add(url);
                _calls.Add($"approve {url}");
                return Task.FromResult(_approval);
            });
        provider.Setup(p => p.MergePullRequestAsync(It.IsAny<string>(), It.IsAny<BranchName>(), It.IsAny<BranchProtection>(), It.IsAny<CancellationToken>()))
            .Returns<string, BranchName, BranchProtection, CancellationToken>((url, _, mode, _) =>
            {
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
            .Setup(r => r.RequeueNotStartedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((url, _) =>
            {
                _calls.Add($"requeue {url}");
                return Task.FromResult(1);
            });
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((e, _) => { if (e is PullRequestOutcomeEvent o) _outcomes.Add(o); })
            .Returns(Task.CompletedTask);
        var completer = new PullRequestCompleter(factory.Object, events.Object, NullLogger<PullRequestCompleter>.Instance);
        var handler = new CompletePullRequestsHandler(new PullRequestMergeSequence(completer, new PullRequestApprovalStep(factory.Object),
            new PullRequestBaseGuard(factory.Object), NullLogger<PullRequestMergeSequence>.Instance,
            new UnmetTicketRequirements(_edges.Object, _landings.Object, Mock.Of<ISpecApprovalStore>()),
            new PullRequestDraftStep(factory.Object),
            new PullRequestBuildRequeue(factory.Object, NullLogger<PullRequestBuildRequeue>.Instance)), completer);
        return await handler.ExecuteAsync(new CompletePullRequestsContext(policy, new BranchName("agent-smith/42"), repos, pipeline,
            "gh", "github-42", seriesBranch), CancellationToken.None);
    }
}
