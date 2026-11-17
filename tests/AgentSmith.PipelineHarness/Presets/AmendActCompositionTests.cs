using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.PipelineHarness.Composition;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-11-17-e061c: a filed ticket's own conversation approves a new spec and then a comment — through the REAL server
/// composition, ONE 'amend' run with a run id records the series and writes it to the ticket branch, and ONE 'comment'
/// run posts the comment. The run list shows both runs with their steps.
/// </summary>
[Trait("Category", "PipelineHarness")]
public sealed class AmendActCompositionTests
{
    private const string Project = "fixture-file";
    private static readonly DateTimeOffset Noon = new(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AmendThenComment_OnANeverWorkedTicket_RunAsAmendAndCommentPipelinesTheRunListShows()
    {
        var (tracker, events) = (new ActTracker(), new ActRunEvents());
        await using var harness = RealCompositionHarness.Build(FixturePaths.For(FixturePaths.Filing), services =>
        {
            services.RemoveAll<ITicketProviderFactory>().AddSingleton<ITicketProviderFactory>(new ActTrackerFactory(tracker));
            services.RemoveAll<IEventPublisher>().AddSingleton<IEventPublisher>(events);
        });
        await using var scope = harness.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        (await sp.GetRequiredService<IFilingRun>().RunAsync(State("s-file"), Spec("first"), true, "prop-1")).Filed.Should().ContainSingle();
        await sp.GetRequiredService<SpecDialogTicketTextRepository>().SaveAsync(new SpecDialogTicketText
            { SessionId = "s-ticket", TicketId = "1", Title = "Title 1", Text = "body", Fingerprint = "fp", ReadAt = Noon }, default);

        var amended = await sp.GetRequiredService<TicketAmendment>().ApplyAsync(State("s-ticket"), Spec("second"), default);
        var project = sp.GetRequiredService<AgentSmith.Server.Services.Tickets.TicketProjects>().Of(Project)!;
        var commented = await sp.GetRequiredService<TicketCommentPost>().PostAsync(project, "1", new TicketCommentOutcome("Hold this."), default);

        amended.Recorded.Should().BeTrue(amended.Notice);
        var record = await sp.GetRequiredService<ISpecApprovalStore>().GetAsync("github-filing", "github-1", default);
        record!.Set.Goal.Should().Be("Goal of second");
        commented.Acted.Should().BeTrue(commented.Notice);
        tracker.Comments.Should().ContainSingle(c => c.Ticket == "1" && c.Text.Contains("Hold this."));
        events.Started.Select(e => e.Pipeline).Should().Equal(PipelinePresets.FileName, PipelinePresets.AmendName, PipelinePresets.CommentName);
        var listed = await events.ListedRunsAsync(harness.Services);
        listed.Single(r => r.Pipeline == PipelinePresets.AmendName).Steps.Select(s => s.CommandName).Should().Contain(CommandNames.AmendTicket);
        listed.Single(r => r.Pipeline == PipelinePresets.CommentName).Steps.Select(s => s.CommandName).Should().Contain(CommandNames.PostTicketComments);
    }

    private static SpecOutcome Spec(string label) =>
        new(new SpecDraft($"{label}-spec", $"Goal of {label}", $"spec: {label}-spec\ngoal: \"Goal of {label}\"\n", []));

    private static ConversationState State(string session) => new()
    {
        JobId = session, ChannelId = $"d-{session}", ThreadId = $"d-{session}", UserId = "sample.user", Platform = "dashboard",
        Project = Project, TicketId = string.Empty, StartedAt = Noon,
        Transcript =
        [
            new TranscriptTurn(TranscriptRole.User, "One spec.", Noon, Author: "sample.user"),
            new TranscriptTurn(TranscriptRole.User, "approve", Noon.AddMinutes(1), Decision: SpecDialogDecision.Approved,
                DecisionProposalId: "prop-1"),
        ],
    };
}
