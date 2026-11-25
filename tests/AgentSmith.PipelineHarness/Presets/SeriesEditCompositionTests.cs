using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-25-9992f: the series master re-cuts its series through the REAL server composition — add_ticket files a
/// prerequisite through a 'file' run that joins the series, with its edges; link_tickets removes an edge; the next series
/// read of the project's edges (SeriesRunProjectReads, the one the series' state reads them through) shows the new requires.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class SeriesEditCompositionTests
{
    private const string Project = "fixture-file";

    [Fact]
    public async Task SeriesMaster_AddsAPrerequisiteAndRelinks_TheNextReadShowsTheNewRequires()
    {
        var rig = new SeriesRig(new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero));
        await using var harness = RealCompositionHarness.Build(
            FixturePaths.For(FixturePaths.Filing), SandboxBackend.Stub, session: null, SkillsBackend.Fixture, rig.Bind);
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var state = await sp.GetRequiredService<SpecDialogSessionManager>().OpenAsync(
            "dashboard", "d-edit", "d-edit", "sample.user", new ActiveScope { Project = Project }, default);
        await sp.GetRequiredService<IFilingRun>().RunAsync(state, new Contracts.Models.TicketGraphOutcome(
        [
            FilePipelineCompositionTests.Ticket("api", []),
            FilePipelineCompositionTests.Ticket("ui", ["api"]),
            FilePipelineCompositionTests.Ticket("docs", ["api"]),
        ]), true, "prop-1");
        var store = sp.GetRequiredService<ISeriesRunStore>();
        var series = (await store.ActiveForConversationAsync(state.JobId, default))!;

        await sp.GetRequiredService<DashboardDialogDispatcher>().DispatchAsync(
            "d-edit", "The ui needs a lookup index first, and the docs need nothing.", "sample.user", true, Project, default);
        harness.ChatClient
            .EnqueueToolCall("add_ticket", """{"title":"Lookup index","body":"Add the lookup index the ui reads.","requires":["1"],"requiredBy":["2"],"reason":"the ui needs it"}""")
            .EnqueueToolCall("link_tickets", """{"ticket":"3","removeRequires":["1"],"reason":"the docs need nothing"}""")
            .EnqueueText("I added #4 before #2 and freed #3.");
        var beat = await sp.GetRequiredService<IActRun>().RunAsync(Project, PipelinePresets.SeriesRunName,
            new Dictionary<string, object> { [ContextKeys.SeriesRunId] = series.Id });
        beat.IsSuccess.Should().BeTrue(beat.Message);

        rig.Tracker.Created.Should().HaveCount(4).And.Subject.Last().Title.Should().Be("Lookup index");
        var kept = (await store.GetAsync(series.Id, default))!;
        kept.Tickets.Should().Contain(t => t.TicketId == "4" && !t.FollowUp, "the master's ticket is a member, not the check's follow-up");
        kept.Acts.Select(a => (a.Tool, a.Ticket)).Should().Equal(("add_ticket", "4"), ("link_tickets", "3"));
        var project = sp.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketProjects>().Of(Project)!;
        var read = await sp.GetRequiredService<AgentSmith.Server.Services.Series.SeriesRunProjectReads>().ReadAsync(project, kept.Tickets, default);
        read.Requires["2"].Should().BeEquivalentTo(["1", "4"]);
        read.Requires["4"].Should().Equal("1");
        read.Requires.Should().NotContainKey("3", "its one edge was removed");
        (await sp.GetRequiredService<ITicketRequirementStore>().ByProjectAsync(Project, default))
            .Where(e => e.TicketId is "4" or "2").Should().OnlyContain(e => e.StartsWhenMet, "the new edges copy their dependents' StartsWhenMet");
    }
}
