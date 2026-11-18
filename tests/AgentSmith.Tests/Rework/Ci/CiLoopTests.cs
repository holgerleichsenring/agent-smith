using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.Rework.Records;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentSmith.Tests.Rework.Ci;

/// <summary>
/// 2026-11-17-3435c: the CI loop through the server's own refresh over a real store, the host scripted: a pull request
/// opened -> CI pending (nothing) -> red (one Sweep nudge; the rework worker's act carries the evidence) -> the fix
/// attempt pushes a new head -> pending -> green (one merge-green run for the fix attempt) -> merged -> landed.
/// </summary>
public sealed class CiLoopTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();
    private readonly List<CiGreenMergeRequest> _merges = [];
    private string _head = "abc";
    private string _state = PrReviewStates.Open;
    private PullRequestCi _ci = new(CiStates.Pending, "abc", null, []);
    private static readonly ResolvedProject Project = new()
    {
        Name = "p", Tracker = PrReviewRefreshHarness.Project.Tracker, Repos = PrReviewRefreshHarness.Project.Repos,
        Merge = new MergePolicyConfig { When = MergeWhen.OnCiGreen },
    };

    public CiLoopTests()
    {
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(PrReviewRefreshHarness.Pr1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new PrReviewState("1", _state, PrReviewRefreshHarness.T, [], _head));
        _h.Provider.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(PrReviewRefreshHarness.Pr1, It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => _ci);
        _h.MergeRuns.Setup(r => r.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ResolvedProject, CiGreenMergeRequest, CancellationToken>((_, r, _) => _merges.Add(r)).ReturnsAsync(true);
    }

    [Fact]
    public async Task OpenedPendingRedFixGreenMergedLanded()
    {
        await _h.RunAsync("r1", PrReviewRefreshHarness.Pr1);
        var first = Attempt("r1", cause: null, PrReviewRefreshHarness.T);

        await StepAsync();
        (await NudgesAsync()).Should().BeEmpty("pending asks nothing");

        _ci = new PullRequestCi(CiStates.Red, "abc", null, [new CiFailure("test", "https://ci/1", "CalcTests.Add: expected 3, was 4")]);
        await StepAsync();
        (await NudgesAsync()).Should().ContainSingle(n => n.Origin == (int)ReworkNudgeOrigin.Sweep);
        var act = await new CiReworkAct(_h.Store.ScopeFactory).NewestAsync(Project, "7", first, default);
        act!.Act.Channel.Should().Be(ReworkChannel.Ci);
        act.Text.Should().Contain("expected 3, was 4");
        await StepAsync();
        (await NudgesAsync()).Should().ContainSingle("the same red head asks once");

        Attempt("r2", AttemptChannels.Ci, _h.Clock.GetUtcNow().AddMinutes(1));
        _h.Clock.Now = _h.Clock.Now.AddMinutes(10);
        _head = "def";
        _ci = new PullRequestCi(CiStates.Pending, "def", null, []);
        await StepAsync();
        _merges.Should().BeEmpty();

        _ci = new PullRequestCi(CiStates.Green, "def", null, []);
        await StepAsync();
        _merges.Should().ContainSingle().Which.AttemptRunId.Should().Be("r2");

        _state = PrReviewStates.Merged;
        await StepAsync();
        _h.Landings().Should().ContainSingle();
        _merges.Should().ContainSingle();
    }

    private async Task StepAsync()
    {
        _h.Clock.Now = _h.Clock.Now.AddMinutes(1);
        await _h.Refresh().RefreshAsync(Project, "7", PrReviewRefreshHarness.Pr1, CancellationToken.None);
    }

    private PreviousAttempt Attempt(string runId, string? cause, DateTimeOffset started)
    {
        var attempt = new PreviousAttempt(runId, RunStatuses.Success, started, true)
        {
            CauseChannel = cause, FinishedAt = started.AddMinutes(5), CiFixStreak = cause is null ? 0 : 1,
            AwaitingCiUrls = new Dictionary<string, string> { ["api"] = PrReviewRefreshHarness.Pr1 },
            PullRequestUrls = new Dictionary<string, string> { ["api"] = PrReviewRefreshHarness.Pr1 },
        };
        _h.Attempts.Setup(a => a.LatestAsync("p", "7", null, It.IsAny<CancellationToken>())).ReturnsAsync(attempt);
        return attempt;
    }

    private async Task<List<ReworkNudge>> NudgesAsync()
    {
        await using var db = _h.Store.Context();
        return await db.Set<ReworkNudge>().AsNoTracking().ToListAsync();
    }

    public void Dispose() => _h.Dispose();
}
