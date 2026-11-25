using AgentSmith.Application.Models;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewHostFakes;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-24-629ab: Azure's state read says whether auto-complete is set; the record notes when it was lost at a conflict, and
/// once the pull request no longer conflicts the CI loop starts one merge-green run that arms it again, once per head, under
/// every merge mode. A cancel with no conflict, a still conflicting head and a held lease start nothing.
/// </summary>
public sealed class AutoCompleteRearmTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly List<CiGreenMergeRequest> _launched = [];
    private bool? _armed = true;
    private bool? _conflicts = false;
    private string _head = "abc";
    private bool _ran = true;

    public AutoCompleteRearmTests()
    {
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) => new PrReviewState(url.Split('/')[^1], PrReviewStates.Open,
                PrReviewRefreshHarness.T, [], _head) { HasConflicts = _conflicts, AutoCompleteArmed = _armed });
        _h.MergeRuns.Setup(r => r.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>(),
                It.IsAny<ActSlot<IReadOnlyList<PullRequestMergeLine>>?>()))
            .Callback<ResolvedProject, CiGreenMergeRequest, CancellationToken, ActSlot<IReadOnlyList<PullRequestMergeLine>>?>((_, r, _, _) => _launched.Add(r))
            .ReturnsAsync(() => _ran);
        _h.Attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("r1", RunStatuses.Success, PrReviewRefreshHarness.T, true)
            {
                PullRequestUrls = new Dictionary<string, string> { ["api"] = PrReviewRefreshHarness.Pr1 },
            });
    }

    [Fact]
    public void Azure_SaysWhetherAutoCompleteIsSet_OnAnOpenPullRequestOnly()
    {
        var pr = AzurePr(PullRequestStatus.Active);
        AzureReposVoteDecisions.Read(pr, [], "self", "u").AutoCompleteArmed.Should().BeFalse();
        pr.AutoCompleteSetBy = new IdentityRef { Id = "dana" };
        AzureReposVoteDecisions.Read(pr, [], "self", "u").AutoCompleteArmed.Should().BeTrue();
        pr.Status = PullRequestStatus.Completed;
        AzureReposVoteDecisions.Read(pr, [], "self", "u").AutoCompleteArmed.Should().BeNull();
    }

    [Fact]
    public void Copy_GoneAtAConflict_IsLost_GoneFromACleanHead_IsACancel_ArmedOrEnded_Clears()
    {
        var stored = Stored(armed: true, conflicts: false);
        Copy(stored, armed: false, conflicts: true);
        stored.AutoCompleteLostAtTicks.Should().Be(9);

        stored = Stored(armed: true, conflicts: true);
        Copy(stored, armed: false, conflicts: false);
        stored.AutoCompleteLostAtTicks.Should().Be(9, "the read before saw the conflict");

        stored = Stored(armed: true, conflicts: false);
        Copy(stored, armed: false, conflicts: null);
        stored.AutoCompleteLostAtTicks.Should().BeNull("a cancel is never overridden on a guess: the host did not say it conflicts");

        stored = Stored(armed: true, conflicts: false);
        Copy(stored, armed: false, conflicts: false, head: "b", ownHead: true);
        stored.AutoCompleteLostAtTicks.Should().Be(9, "agent-smith pushed the new head: a refresh before any read saw the conflict, a rework, a CI fix");

        stored = Stored(armed: true, conflicts: false);
        Copy(stored, armed: false, conflicts: false, head: "b");
        stored.AutoCompleteLostAtTicks.Should().BeNull("2026-11-25-098f: no run pushed the new head; auto-merge stays off");
        stored.AutoCompleteGoneHead.Should().Be("b", "the run's record may still be on its way");
        PrRecordAutoComplete.HeadInQuestion(Read(armed: false, head: "c"), stored).Should().Be("b", "the head it went on decides, not a later one");
        Copy(stored, armed: false, conflicts: false, head: "b", ownHead: true);
        (stored.AutoCompleteLostAtTicks, stored.AutoCompleteGoneHead).Should().Be(((long?)9, (string?)null), "the run's record arrived");

        stored = Stored(armed: true, conflicts: true);
        Copy(stored, armed: false, conflicts: false, head: "b");
        stored.AutoCompleteLostAtTicks.Should().BeNull("a person's push to a conflicting pull request is still a person's push");

        stored = Stored(armed: true, conflicts: false);
        Copy(stored, armed: false, conflicts: false);
        stored.AutoCompleteLostAtTicks.Should().BeNull("a person took it off a head that merges cleanly");

        stored = Stored(armed: false, conflicts: false, lostAt: 5);
        Copy(stored, armed: false, conflicts: false);
        stored.AutoCompleteLostAtTicks.Should().Be(5, "still waiting to be armed again");
        Copy(stored, armed: null, conflicts: false);
        stored.AutoCompleteLostAtTicks.Should().Be(5, "a host that does not say keeps what was stored");
        Copy(stored, armed: true, conflicts: false);
        (stored.AutoCompleteArmed, stored.AutoCompleteLostAtTicks).Should().Be(((bool?)true, (long?)null));

        stored = Stored(armed: false, conflicts: false, lostAt: 5);
        PrReviewRecordCopy.Record(new PrReviewRecord { State = PrReviewStates.Merged, CheckedAtTicks = 9 }, stored);
        (stored.AutoCompleteArmed, stored.AutoCompleteLostAtTicks).Should().Be(((bool?)null, (long?)null));
    }

    [Theory]
    [InlineData(ReviewMode.End)]
    [InlineData(ReviewMode.None)]
    public async Task LostAtAConflict_IsArmedAgainOnce_WhenItNoLongerConflicts(ReviewMode when)
    {
        await LoseAtAConflictAsync();
        await RefreshAsync(when);
        _launched.Should().BeEmpty("a pull request that conflicts cannot complete");

        (_head, _conflicts) = ("def", null); // the refresh pushed; Azure has not merged the new head yet
        await RefreshAsync(when);
        _launched.Should().BeEmpty("only a head the host says merges cleanly is armed again");

        _conflicts = false;
        await RefreshAsync(when);
        await RefreshAsync(when);

        var request = _launched.Should().ContainSingle().Subject;
        request.PullRequests.Should().ContainSingle().Which.Should().Match<CiGreenPullRequest>(p => p.Rearm && !p.NotStarted && !p.NoCi);
        _h.Records().Single().AutoCompleteRearmedHead.Should().Be("def");
    }

    // 2026-11-25-098f: GitLab cancels auto-merge on every new commit; the record arms it again only after agent-smith's own push.
    [Fact]
    public async Task CancelledByAgentSmithsOwnPush_IsArmedAgain()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        await PushedByAgentSmithAsync("def");
        (_head, _armed) = ("def", false);
        await RefreshAsync();

        _h.Records().Single().AutoCompleteLostAtTicks.Should().NotBeNull();
        _launched.Should().ContainSingle().Which.PullRequests.Should().ContainSingle(p => p.Rearm);
        _h.Records().Single().AutoCompleteRearmedHead.Should().Be("def");
    }

    [Fact]
    public async Task CancelledByAPersonsPush_IsNotArmedAgain()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        await PushedByAgentSmithAsync("abc");
        (_head, _armed) = ("def", false);
        await RefreshAsync();
        await RefreshAsync();

        _h.Records().Single().AutoCompleteLostAtTicks.Should().BeNull("agent-smith never pushed def");
        _launched.Should().BeEmpty();

        await PushedByAgentSmithAsync("ghi");
        _head = "ghi";
        await RefreshAsync();
        _launched.Should().BeEmpty("agent-smith's later push on top does not make the person's cancelling push its own");
    }

    [Fact]
    public async Task OwnPushRecordedAfterTheRead_IsArmedAgain_AtTheNextRead()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        (_head, _armed) = ("def", false);
        await RefreshAsync();
        _launched.Should().BeEmpty("the run's record of def has not landed yet");

        await PushedByAgentSmithAsync("def");
        await RefreshAsync();

        _launched.Should().ContainSingle();
        _h.Records().Single().Should().Match<PrReviewRecord>(r => r.AutoCompleteGoneHead == null && r.AutoCompleteRearmedHead == "def");
    }

    [Fact]
    public async Task AShaPushedInAnotherProject_IsNotOurs()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await using (var ctx = _h.Store.Context())
        {
            ctx.Add(new Run { Id = "other", Project = "q", TicketId = "1", Pipeline = "code", Status = "success", StartedAt = PrReviewRefreshHarness.T });
            await ctx.SaveChangesAsync();
        }
        await PushedInAsync("other", "def");
        await RefreshAsync();
        (_head, _armed) = ("def", false);
        await RefreshAsync();

        _launched.Should().BeEmpty();
    }

    [Fact]
    public async Task ACancelWithoutAConflict_OrAHeldLease_StartNothing()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        await RefreshAsync();
        _armed = false;
        await RefreshAsync();
        _launched.Should().BeEmpty("a person took auto-complete off");

        await LoseAtAConflictAsync(run: false);
        _conflicts = false;
        _h.Leases.Setup(l => l.GetByTicketAsync("p", new TicketId("7"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentSmith.Contracts.Models.StaleLease("p", new TicketId("7"), "other", null));
        await RefreshAsync();
        _launched.Should().BeEmpty();
        _h.Records().Single().AutoCompleteRearmedHead.Should().BeNull("a run that did not run marks nothing");
    }

    [Fact]
    public async Task ALostRecord_StaysDueInTheCiWatch_UntilItIsArmedAgain()
    {
        await LoseAtAConflictAsync();
        _conflicts = false;
        await RefreshAsync();
        Due().Should().BeEmpty("it was armed again on this head");

        await using (var ctx = _h.Store.Context())
        {
            var row = ctx.Set<PrReviewRecord>().Single();
            row.AutoCompleteRearmedHead = null;
            await ctx.SaveChangesAsync();
        }
        Due().Should().ContainSingle("a lost auto-complete is read every merge.ci_poll_seconds in every mode");
    }

    [Fact]
    public async Task ADraft_OrAStillConflictingHead_IsNotArmedAgain_NorDue()
    {
        await LoseAtAConflictAsync();
        Due().Should().BeEmpty("it still conflicts");

        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string url, CancellationToken _) => new PrReviewState(url.Split('/')[^1], PrReviewStates.Open,
                PrReviewRefreshHarness.T, [], _head) { HasConflicts = false, AutoCompleteArmed = false, IsDraft = true });
        await RefreshAsync();

        _launched.Should().BeEmpty();
        Due().Should().BeEmpty("a draft cannot complete");
    }

    private IReadOnlyList<PrReviewRecordView> Due()
    {
        using var scope = _h.Store.ScopeFactory.CreateScope();
        return scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>()
            .CiDueAsync("p", new PrRecordCiDue(_h.Clock.Now.AddHours(1).UtcTicks, 0, ActsOnVerdict: false), CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    // Armed, then a sibling merged: the host says it conflicts and drops auto-complete.
    private async Task LoseAtAConflictAsync(bool run = true)
    {
        if (run) await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        (_armed, _conflicts) = (true, false);
        await RefreshAsync();
        (_armed, _conflicts) = (false, true);
        await RefreshAsync();
        _h.Records().Single().AutoCompleteLostAtTicks.Should().NotBeNull();
    }

    private async Task RefreshAsync(ReviewMode when = ReviewMode.End)
    {
        _h.Clock.Now = _h.Clock.Now.AddSeconds(1);
        await _h.Refresh().RefreshAsync(Project(when), "7", null, CancellationToken.None);
    }

    private static PrReviewRecord Stored(bool armed, bool conflicts, long? lostAt = null) => new()
    {
        State = PrReviewStates.Open, HeadSha = "a", AutoCompleteArmed = armed, HasConflicts = conflicts, AutoCompleteLostAtTicks = lostAt,
    };

    private static void Copy(PrReviewRecord stored, bool? armed, bool? conflicts, string head = "a", bool ownHead = false) =>
        PrReviewRecordCopy.Record(Read(armed, head, conflicts), stored, ownHead);

    private static PrReviewRecord Read(bool? armed, string head, bool? conflicts = false) => new()
    {
        State = PrReviewStates.Open, HeadSha = head, AutoCompleteArmed = armed, HasConflicts = conflicts, CheckedAtTicks = 9,
    };

    // A run recorded the heads it pushed: its BranchPushedEvents, projected onto Runs.PushedHeadsJson as the applier does.
    private async Task PushedByAgentSmithAsync(params string[] heads) => await PushedInAsync("r1", heads);

    private async Task PushedInAsync(string runId, params string[] heads)
    {
        var applier = RunEventAppliers.Default();
        foreach (var head in heads)
        {
            await using var uow = _h.Store.Context();
            await applier.ApplyAsync(uow, new BranchPushedEvent(runId, PrReviewRefreshHarness.T, "agent-smith/7", head), CancellationToken.None);
        }
    }

    private static ResolvedProject Project(ReviewMode when) => new()
    {
        Name = "p", Tracker = PrReviewRefreshHarness.Project.Tracker, Repos = PrReviewRefreshHarness.Project.Repos,
        Review = when,
    };

    public void Dispose() => _h.Dispose();
}
