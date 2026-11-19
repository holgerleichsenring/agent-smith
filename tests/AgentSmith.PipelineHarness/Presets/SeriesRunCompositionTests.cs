using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-0207d: a series run through the REAL server composition — a Work it out conversation files three tickets
/// with requires, the 'file' run opens the series, its beats report into the conversation, a recommended question is
/// taken at its deadline, the last landing starts the series check, one failing criterion is filed as a follow-up into
/// the series and started, and the check's summary closes the account. The tickets' facts, the queued launch and the
/// clock are the harness boundaries: the beats and checks the sweep would queue run here in turn.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesRunCompositionTests
{
    private const string Project = "fixture-file";
    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeriesRun_FiledToVerified_ReportsAsksDefaultsChecksAndFollowsUp()
    {
        var rig = new SeriesRig(Noon);
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Filing), SandboxBackend.Stub, session: null, SkillsBackend.Fixture, rig.Bind);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var state = await sp.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            "dashboard", "d-series", "d-series", "sample.user", new ActiveScope { Project = Project }, default);

        var report = await sp.GetRequiredService<IFilingRun>().RunAsync(state, List(), true, "prop-1");
        report.Filed.Should().HaveCount(3);
        var series = (await sp.GetRequiredService<ISeriesRunStore>().ActiveForConversationAsync(state.JobId, default))!;
        series.Tickets.Select(t => t.TicketId).Should().Equal("1", "2", "3");

        rig.Facts.Set("1", RunStatuses.Running, finished: false);
        await BeatAsync(sp, series.Id);
        (await TextsAsync(sp)).Should().Contain(t => t.StartsWith("Filed 3 tickets. #1 started; 2 wait on their prerequisites"))
            .And.NotContain(t => t.Contains("| #2 |"), "2026-11-19-1b72a: the dashboard's Series pane holds the table");

        rig.Facts.Set("1", RunStatuses.Success, prOpened: true);
        rig.Facts.Set("2", RunStatuses.Running, finished: false);
        await BeatAsync(sp, series.Id);
        (await TextsAsync(sp)).Last().Should().Be("#1's pull request waits for your merge.", "only what needs the operator is written");

        await sp.GetRequiredService<DashboardDialogDispatcher>().DispatchAsync(
            "d-series", "Should forwarding keep the deadline?", "sample.user", true, Project, default);
        harness.ChatClient
            .EnqueueToolCall("ask_human", """{"question":"Keep the original deadline when forwarding?","choices":[{"label":"keep"},{"label":"restart"}],"recommended_index":0}""")
            .EnqueueText("I asked whether forwarding keeps the deadline.");
        await BeatAsync(sp, series.Id);
        (await TextsAsync(sp)).Should().Contain(t => t.Contains("Recommended: keep") && t.Contains("I take the recommendation at 14:00 UTC"));
        harness.ChatClient.LastOptions!.Tools!.Select(t => t.Name).Should().Contain(["ask_human", "tell_ticket", "read_run", "read_run_events"],
            "the master reads runs through the admin assistant's curated reads");

        rig.Clock.Advance(TimeSpan.FromMinutes(121));
        harness.ChatClient.EnqueueText("Going with keep: forwarding keeps the original deadline.");
        await BeatAsync(sp, series.Id);
        (await TextsAsync(sp)).Should().Contain(t => t.StartsWith("Taken by default at 14:01 UTC: keep"))
            .And.Contain("Going with keep: forwarding keeps the original deadline.");

        foreach (var id in new[] { "1", "2", "3" }) rig.Facts.Land(id);
        await BeatAsync(sp, series.Id);
        rig.Launches.Should().ContainSingle(l => l.Pipeline == PipelinePresets.SeriesVerifyName);

        harness.ChatClient
            .EnqueueToolCall("record_criterion", """{"number":1,"holds":true,"evidence":"dotnet test: ApiTests passed"}""")
            .EnqueueToolCall("record_criterion", """{"number":2,"holds":false,"evidence":"expired requests are not notified after a restart"}""")
            .EnqueueText("1 of 2 hold.");
        var check = await sp.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesVerifyName,
            new Dictionary<string, object> { [ContextKeys.SeriesRunId] = series.Id });
        check.IsSuccess.Should().BeTrue(check.Message);
        rig.Tracker.Created.Should().HaveCount(4).And.Subject.Last().Title.Should().Contain("ui works");

        rig.Facts.Set("4", RunStatuses.Running, finished: false);
        await BeatAsync(sp, series.Id);
        var summary = (await TextsAsync(sp)).Last(t => t.StartsWith("All 4 tickets landed") || t.StartsWith("All 3 tickets landed"));
        summary.Should().Contain("1 of 2 hold").And.Contain("I filed #4 for it in this series; it is running").And.EndWith("Nothing waits on you.");
        var kept = (await sp.GetRequiredService<ISeriesRunStore>().GetAsync(series.Id, default))!;
        kept.State.Should().Be(SeriesRunStates.Running);
        kept.Tickets.Last().Should().Match<SeriesTicket>(t => t.TicketId == "4" && t.FollowUp);
        kept.Verdicts.Last().FollowUpStart.Should().BeNull("the follow-up is started like any filed ticket");
    }

    private static async Task BeatAsync(IServiceProvider sp, string series)
    {
        var beat = await sp.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesRunName,
            new Dictionary<string, object> { [ContextKeys.SeriesRunId] = series });
        beat.IsSuccess.Should().BeTrue(beat.Message);
    }

    private static async Task<IReadOnlyList<string>> TextsAsync(IServiceProvider sp) =>
        [.. (await sp.GetRequiredService<SpecDialogSessionManager>().GetOpenByThreadAsync("dashboard", "d-series", default))!
            .Transcript.Where(t => t.Role == TranscriptRole.Assistant).Select(t => t.Text)];

    private static Contracts.Models.TicketGraphOutcome List() => new(
    [
        FilePipelineCompositionTests.Ticket("api", []),
        FilePipelineCompositionTests.Ticket("ui", ["api"]),
        FilePipelineCompositionTests.Ticket("docs", ["api"]),
    ]);
}
