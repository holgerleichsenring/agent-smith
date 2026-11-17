using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.Tickets.Closing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-e061a: a filed ticket closed and reopened through the REAL server composition — the close act validates,
/// then ONE 'close' run with a run id closes the tracker ticket and stores the mark; the reopen is ONE 'reopen' run that
/// clears it. Both runs are rows the dashboard's run list reads. The tracker is the harness boundary.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class CloseActCompositionTests
{
    private const string Project = "fixture-file";
    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CloseThenReopen_AFiledTicket_RunAsCloseAndReopenPipelinesTheRunListShows()
    {
        var (tracker, events) = (new ActTracker(), new ActRunEvents());
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new ActTrackerFactory(tracker));
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
            services.AddDashboardApi(); // the ticket page's close and reopen
        });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        (await sp.GetRequiredService<IFilingRun>().RunAsync(State(), Spec(), true, "prop-1")).Filed.Should().ContainSingle();
        var act = sp.GetRequiredService<TicketCloseAct>();

        var closed = await act.CloseAsync(Project, "1", new TicketCloseRequest("done elsewhere", false, CloseOnTracker: true), "sample.user", default);
        var marks = sp.GetRequiredService<ITicketClosedMarkStore>();
        var mark = (await marks.OfAsync("github-filing", ["github-1"], default)).Should().ContainSingle().Subject;
        var reopened = await act.ReopenAsync(Project, "1", default);

        closed.Refusal.Should().BeNull();
        ((TicketCloseAnswer)closed.Body!).Tracker!.Closed.Should().BeTrue();
        tracker.Closed.Should().ContainSingle().Which.Should().Be(("1", "Agent Smith — closed by sample.user: done elsewhere"));
        mark.TrackerClosed.Should().BeTrue();
        reopened.Refusal.Should().BeNull();
        (await marks.OfAsync("github-filing", ["github-1"], default)).Should().BeEmpty();
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName, PipelinePresets.CloseName, PipelinePresets.ReopenName);
        events.Started.Should().OnlyContain(e => !string.IsNullOrEmpty(e.RunId));
        var listed = await events.ListedRunsAsync(harness.Services);
        listed.Single(r => r.Pipeline == PipelinePresets.CloseName).Steps.Select(s => s.CommandName).Should().Contain(CommandNames.CloseTicket);
        listed.Single(r => r.Pipeline == PipelinePresets.ReopenName).Steps.Select(s => s.CommandName).Should().Contain(CommandNames.ReopenTicket);
    }

    private static SpecOutcome Spec() =>
        new(new SpecDraft("close-spec", "Goal of close", "spec: close-spec\ngoal: \"Goal of close\"\n", []));

    private static ConversationState State() => new()
    {
        JobId = "s-close", ChannelId = "d-close", ThreadId = "d-close", UserId = "sample.user", Platform = "dashboard",
        Project = Project, TicketId = string.Empty, StartedAt = Noon,
        Transcript =
        [
            new TranscriptTurn(TranscriptRole.User, "One spec.", Noon, Author: "sample.user"),
            new TranscriptTurn(TranscriptRole.User, "approve", Noon.AddMinutes(1), Decision: SpecDialogDecision.Approved,
                DecisionProposalId: "prop-1"),
        ],
    };
}
