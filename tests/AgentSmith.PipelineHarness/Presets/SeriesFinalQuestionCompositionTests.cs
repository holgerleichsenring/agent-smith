using AgentSmith.Application.Services.Series;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-28-d1ffc: delivery starting through the REAL server composition. Seen live: the series check's message was the last
/// thing in the chat — no beat reason is raised when delivery starts, so the master was never called and never asked. Here the
/// check passes, the series moves to Delivering, its final pull request opens, and the beat posts the merge question last, once,
/// with its two quick replies; the master's model is never called for it.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesFinalQuestionCompositionTests
{
    private const string Project = "fixture-file";
    private const string Final = "https://stub.test/filing-fixture/pull/90";

    [Fact]
    public async Task Delivering_PostsTheMergeQuestionLast_Once()
    {
        var rig = new SeriesRig(new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero));
        string? open = null, branch = null;
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Filing), SandboxBackend.Stub, session: null, SkillsBackend.Fixture, services =>
            {
                rig.Bind(services);
                services.RemoveAll<ISourceProviderFactory>().AddSingleton(Host(() => open, url => open = url));
                services.RemoveAll<IPrReviewRecordReader>().AddSingleton(Landings(() => branch));
            });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var store = sp.GetRequiredService<ISeriesRunStore>();
        var state = await sp.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            "dashboard", "d-final", "d-final", "sample.user", new ActiveScope { Project = Project }, default);
        await sp.GetRequiredService<IFilingRun>().RunAsync(state, new Contracts.Models.TicketGraphOutcome(
            [FilePipelineCompositionTests.Ticket("api", []), FilePipelineCompositionTests.Ticket("ui", ["api"])]), true, "prop-1");
        var series = (await store.ActiveForConversationAsync(state.JobId, default))!;
        branch = series.IntegrationBranch;
        branch.Should().NotBeNull("review end integrates on a series branch");
        foreach (var ticket in series.Tickets) rig.Facts.Land(ticket.TicketId);
        await store.UpdateAsync(series.Id, r => r with
        {
            State = SeriesRunStates.Verifying, VerifyRunId = "run-check",
            Verdicts = [new SeriesVerdict("run-check", rig.Clock.GetUtcNow(), [new("#1 · api-spec: api works", true, "dotnet test passed")])],
        }, default);
        var calls = harness.ChatClient.CallMessages.Count;

        await BeatAsync(sp, series.Id);

        var texts = await TextsAsync(sp);
        texts[^1].Should().Contain(SeriesMergeQuestion.FinalText).And.Contain($"({Final})")
            .And.Contain($"Choices: {SeriesAskTool.MergeIt} / {SeriesAskTool.LookFirst}");
        texts.Should().Contain(t => t.StartsWith("All 2 tickets landed. Series check on")
            && t.Contains("Waiting on you: your review of the final pull request."));
        var kept = (await store.GetAsync(series.Id, default))!;
        kept.State.Should().Be(SeriesRunStates.Delivering);
        kept.Question!.Should().Match<SeriesQuestion>(q => q.MergeOf == "final" && q.NoDefault && q.Answer == null);
        harness.ChatClient.CallMessages.Count.Should().Be(calls, "asking the merge question costs no model call");

        rig.Clock.Advance(TimeSpan.FromMinutes(5));
        await BeatAsync(sp, series.Id);
        (await TextsAsync(sp)).Count(t => t.Contains(SeriesMergeQuestion.FinalText)).Should().Be(1, "it is asked once");
    }

    private static ISourceProviderFactory Host(Func<string?> open, Action<string> opened)
    {
        var host = new Mock<ISourceProvider>();
        host.As<IWorkBranchCleanup>().Setup(h => h.ListWorkBranchesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        host.As<IWorkBranchCleanup>().Setup(h => h.PullRequestsOfAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new BranchPullRequests(open() is null ? [] : [new BranchPullRequest(BranchPullRequestState.Open, "h1")], 0));
        host.Setup(h => h.FindOpenPullRequestAsync(It.IsAny<Repository>(), It.IsAny<CancellationToken>())).ReturnsAsync(open);
        host.Setup(h => h.CreatePullRequestAsync(It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<PullRequestTicket?>(), It.IsAny<bool>(), It.IsAny<BranchName?>()))
            .ReturnsAsync(() => { opened(Final); return Final; });
        return Mock.Of<ISourceProviderFactory>(f => f.Create(It.IsAny<AgentSmith.Contracts.Models.Configuration.RepoConnection>()) == host.Object);
    }

    // The ticket's pull request merged into the series branch, as its stored review record says.
    private static IPrReviewRecordReader Landings(Func<string?> branch)
    {
        var records = new Mock<IPrReviewRecordReader>();
        records.Setup(r => r.ByTicketAsync(Project, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string ticket, CancellationToken _) =>
            [
                new PrReviewRecordView("k", "https://stub.test/filing-fixture/pull/7", "filing-fixture", "7", Project, ticket, "r",
                    PrReviewStates.Merged, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, []) { TargetBranch = branch() },
            ]);
        return records.Object;
    }

    private static async Task BeatAsync(IServiceProvider sp, string series)
    {
        var beat = await sp.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesRunName,
            new Dictionary<string, object> { [ContextKeys.SeriesRunId] = series });
        beat.IsSuccess.Should().BeTrue(beat.Message);
    }

    private static async Task<IReadOnlyList<string>> TextsAsync(IServiceProvider sp) =>
        [.. (await sp.GetRequiredService<SpecDialogSessionManager>().GetOpenByThreadAsync("dashboard", "d-final", default))!
            .Transcript.Where(t => t.Role == TranscriptRole.Assistant).Select(t => t.Text)];
}
